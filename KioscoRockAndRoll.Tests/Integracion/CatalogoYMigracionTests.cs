using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class CatalogoYMigracionTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly InventarioServicio _inventario;
    private readonly CatalogoServicio<Categoria> _categorias;
    private readonly CatalogoServicio<Proveedor> _proveedores;

    public CatalogoYMigracionTests()
    {
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        _categorias = new CatalogoServicio<Categoria>(new SqliteCategoriaRepositorio(_base.Conexion));
        _proveedores = new CatalogoServicio<Proveedor>(new SqliteProveedorRepositorio(_base.Conexion));
    }

    public void Dispose() => _base.Dispose();

    private Categoria NuevaCategoria(string nombre)
    {
        var categoria = new Categoria(nombre);
        _categorias.Guardar(categoria);
        return categoria;
    }

    private Proveedor NuevoProveedor(string nombre, string telefono = "")
    {
        var proveedor = new Proveedor(nombre, telefono);
        _proveedores.Guardar(proveedor);
        return proveedor;
    }

    [Fact]
    public void Catalogo_GuardaListaOrdenadaYModifica()
    {
        NuevaCategoria("Golosinas");
        var bebidas = NuevaCategoria("Bebidas");
        var sur = NuevoProveedor("Distribuidora Sur", "11 5555-1234");

        Assert.Equal(["Bebidas", "Golosinas"], _categorias.Listar().Select(c => c.Nombre));
        Assert.Equal("11 5555-1234", _proveedores.ObtenerPorId(sur.Id)!.Telefono);

        bebidas.Renombrar("Bebidas frías");
        _categorias.Guardar(bebidas);
        Assert.Equal("Bebidas frías", _categorias.ObtenerPorId(bebidas.Id)!.Nombre);
    }

    [Fact]
    public void Catalogo_NoPermiteNombresRepetidos_SinImportarMayusculas()
    {
        NuevaCategoria("Bebidas");
        Assert.Throws<ReglaDeNegocioException>(() => NuevaCategoria("BEBIDAS"));
    }

    [Fact]
    public void Producto_GuardaCategoriaYProveedor_YSeFiltra()
    {
        var bebidas = NuevaCategoria("Bebidas");
        var sur = NuevoProveedor("Sur");
        var coca = _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 10, 2, bebidas.Id, sur.Id));
        _inventario.Crear(new DatosProducto("2", "Alfajor", 400, 700, 10, 2));

        var leida = _inventario.ObtenerPorId(coca.Id)!;
        Assert.Equal(bebidas.Id, leida.CategoriaId);
        Assert.Equal(sur.Id, leida.ProveedorId);

        var deBebidas = _inventario.Buscar(new FiltroProductos { Categoria = FiltroClasificacion.Solo(bebidas.Id) });
        var sinCategoria = _inventario.Buscar(new FiltroProductos { Categoria = FiltroClasificacion.SinAsignar });
        Assert.Equal("Coca", Assert.Single(deBebidas).Nombre);
        Assert.Equal("Alfajor", Assert.Single(sinCategoria).Nombre);
    }

    [Fact]
    public void EliminarCategoria_DejaLosProductosSinCategoria()
    {
        var bebidas = NuevaCategoria("Bebidas");
        var coca = _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 10, 2, bebidas.Id));
        Assert.Equal(1, _categorias.ContarProductos(bebidas.Id));

        _categorias.Eliminar(bebidas.Id);

        Assert.Empty(_categorias.Listar());
        var leida = _inventario.ObtenerPorId(coca.Id);
        Assert.NotNull(leida); // el producto no se borra
        Assert.Null(leida.CategoriaId);
    }

    [Fact]
    public void ArmarPedido_ConLosProductosDelProveedor()
    {
        var sur = NuevoProveedor("Sur");
        var norte = NuevoProveedor("Norte");
        _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, Stock: 1, StockMinimo: 6, ProveedorId: sur.Id));
        _inventario.Crear(new DatosProducto("2", "Pepsi", 900, 1500, Stock: 50, StockMinimo: 6, ProveedorId: sur.Id));
        _inventario.Crear(new DatosProducto("3", "Alfajor", 400, 700, Stock: 0, StockMinimo: 5, ProveedorId: norte.Id));

        var pedido = _inventario.ArmarPedido(sur);

        var linea = Assert.Single(pedido.Lineas);
        Assert.Equal("Coca", linea.Nombre);
        Assert.Equal(11, linea.Cantidad);
    }

    [Fact]
    public void Migracion_ActualizaUnaBaseDeLaVersionAnterior_SinPerderDatos()
    {
        using var vieja = new BaseDeDatosVieja();

        new InicializadorBaseDeDatos(vieja.Conexion).Inicializar();

        using (var db = vieja.Conexion.Abrir())
            Assert.Equal(InicializadorBaseDeDatos.VersionActual, InicializadorBaseDeDatos.LeerVersion(db));

        var inventario = new InventarioServicio(new SqliteProductoRepositorio(vieja.Conexion));
        var coca = inventario.ObtenerPorCodigo("779");
        Assert.NotNull(coca);
        Assert.Equal(1500m, coca.PrecioVenta);
        Assert.Null(coca.CategoriaId);

        // Y ya se pueden usar las tablas nuevas.
        var categorias = new CatalogoServicio<Categoria>(new SqliteCategoriaRepositorio(vieja.Conexion));
        categorias.Guardar(new Categoria("Bebidas"));
        Assert.Single(categorias.Listar());
    }

    [Fact]
    public void Migracion_CorrerDosVeces_NoRompeNada()
    {
        new InicializadorBaseDeDatos(_base.Conexion).Inicializar(); // ya se corrió una vez al crear la base
        NuevaCategoria("Bebidas");
        Assert.Single(_categorias.Listar());
    }

    /// <summary>Una base con el esquema de la versión 1 (como la tenía el programa antes de las categorías).</summary>
    private sealed class BaseDeDatosVieja : IDisposable
    {
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));

        public BaseDeDatosVieja()
        {
            Directory.CreateDirectory(_carpeta);
            Conexion = new ConexionSqlite(Path.Combine(_carpeta, "vieja.db"));

            using var db = Conexion.Abrir();
            using var comando = db.CreateCommand();
            comando.CommandText = """
                CREATE TABLE Productos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, CodigoBarras TEXT NOT NULL UNIQUE, Nombre TEXT NOT NULL,
                    PrecioCostoCentavos INTEGER NOT NULL, PrecioVentaCentavos INTEGER NOT NULL,
                    Stock INTEGER NOT NULL, StockMinimo INTEGER NOT NULL);
                CREATE TABLE Ventas (Id INTEGER PRIMARY KEY AUTOINCREMENT, Fecha TEXT NOT NULL, MedioPago INTEGER NOT NULL);
                CREATE TABLE VentaLineas (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, VentaId INTEGER NOT NULL, ProductoId INTEGER NOT NULL,
                    NombreProducto TEXT NOT NULL, Cantidad INTEGER NOT NULL, PrecioUnitarioCentavos INTEGER NOT NULL);
                INSERT INTO Productos (CodigoBarras, Nombre, PrecioCostoCentavos, PrecioVentaCentavos, Stock, StockMinimo)
                VALUES ('779', 'Coca', 90000, 150000, 10, 2);
                """;
            comando.ExecuteNonQuery();
        }

        public ConexionSqlite Conexion { get; }

        public void Dispose()
        {
            try { Directory.Delete(_carpeta, recursive: true); }
            catch (IOException) { }
        }
    }
}
