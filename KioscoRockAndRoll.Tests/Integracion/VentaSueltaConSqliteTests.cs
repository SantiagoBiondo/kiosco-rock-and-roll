using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class VentaSueltaConSqliteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly InventarioServicio _inventario;
    private readonly VentaServicio _ventas;
    private readonly Producto _atado;
    private readonly Producto _suelto;

    public VentaSueltaConSqliteTests()
    {
        var reloj = TimeProvider.System;
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        var caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        _ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), caja, reloj);
        _atado = _inventario.Crear(new DatosProducto("7790001", "Marlboro x20", 4000, 5000, 3, 1));
        _suelto = _inventario.Crear(new DatosProducto("101", "Cigarrillo suelto", 200, 300, 0, 0,
            CodigoPaquete: "7790001", UnidadesPorPaquete: 20));
        caja.AbrirCaja(0);
    }

    public void Dispose() => _base.Dispose();

    private Producto Leer(Producto producto) => _inventario.ObtenerPorId(producto.Id)!;

    private Venta Vender(Producto producto, int cantidad)
    {
        var carrito = new Carrito();
        carrito.Agregar(Leer(producto), cantidad);
        return _ventas.Cobrar(carrito, Cobro.Crear(carrito.Total, MedioDePago.Efectivo));
    }

    [Fact]
    public void ElProductoSueltoSeGuardaConSuPaquete()
    {
        var suelto = Leer(_suelto);

        Assert.Equal(_atado.Id, suelto.PaqueteId);
        Assert.Equal(20, suelto.UnidadesPorPaquete);
        Assert.Equal(3, suelto.PaquetesCerrados);
        Assert.Equal(60, suelto.StockDisponible);
        Assert.False(suelto.StockBajo);
    }

    [Fact]
    public void VenderUnoSinSueltas_AbreUnPaquete()
    {
        Vender(_suelto, 1);

        Assert.Equal(19, Leer(_suelto).Stock);
        Assert.Equal(2, Leer(_atado).Stock);
    }

    [Fact]
    public void SiQuedanSueltas_NoAbreOtroPaquete()
    {
        Vender(_suelto, 1);
        Vender(_suelto, 5);

        Assert.Equal(14, Leer(_suelto).Stock);
        Assert.Equal(2, Leer(_atado).Stock);
    }

    [Fact]
    public void SinPaquetesCerrados_LasSueltasQuedanEnNegativo()
    {
        Vender(_suelto, 60);    // abre los 3 paquetes
        Vender(_suelto, 2);

        Assert.Equal(-2, Leer(_suelto).Stock);
        Assert.Equal(0, Leer(_atado).Stock);
    }

    [Fact]
    public void VenderElPaqueteEntero_NoTocaLasSueltas()
    {
        Vender(_atado, 1);

        Assert.Equal(2, Leer(_atado).Stock);
        Assert.Equal(0, Leer(_suelto).Stock);
    }

    [Fact]
    public void AnularLaVenta_DevuelveLasUnidadesComoSueltas()
    {
        var venta = Vender(_suelto, 1);

        _ventas.Anular(venta.Id, "Se equivocó");

        Assert.Equal(20, Leer(_suelto).Stock);  // el paquete ya está abierto
        Assert.Equal(2, Leer(_atado).Stock);
    }

    [Fact]
    public void CodigoDePaqueteInexistente_Falla()
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => _inventario.Crear(
            new DatosProducto("102", "Chicle suelto", 10, 20, 0, 0, CodigoPaquete: "999", UnidadesPorPaquete: 10)));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public void UnPaqueteConSueltas_NoPuedeSalirDeOtroPaquete()
    {
        var caja = _inventario.Crear(new DatosProducto("7790002", "Caja de 10 atados", 38000, 0, 1, 0));

        Assert.Throws<ReglaDeNegocioException>(() => _inventario.Modificar(_atado.Id,
            new DatosProducto("7790001", "Marlboro x20", 4000, 5000, 3, 1,
                CodigoPaquete: caja.CodigoBarras, UnidadesPorPaquete: 10)));
    }

    [Fact]
    public void ModificarSinCodigoDePaquete_DejaDeVenderseSuelto()
    {
        _inventario.Modificar(_suelto.Id, new DatosProducto("101", "Cigarrillo suelto", 200, 300, 0, 0));

        Assert.False(Leer(_suelto).SeVendeSuelto);
        Vender(_suelto, 1);
        Assert.Equal(3, Leer(_atado).Stock);
    }
}
