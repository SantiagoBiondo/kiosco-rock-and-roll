using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;
using Microsoft.Data.Sqlite;
using static KioscoRockAndRoll.Datos.Conversiones;

namespace KioscoRockAndRoll.Datos;

public sealed class SqliteProductoRepositorio(ConexionSqlite conexion) : IProductoRepositorio
{
    private const string SelectProductos = """
        SELECT Id, CodigoBarras, Nombre, PrecioCostoCentavos, PrecioVentaCentavos, Stock, StockMinimo
        FROM Productos
        """;

    public Producto? ObtenerPorId(long id) =>
        ConsultarUno($"{SelectProductos} WHERE Id = $id", ("$id", id));

    public Producto? ObtenerPorCodigo(string codigoBarras) =>
        ConsultarUno($"{SelectProductos} WHERE CodigoBarras = $codigo", ("$codigo", codigoBarras));

    public IReadOnlyList<Producto> Buscar(string texto)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        // ESCAPE permite buscar textos que contienen % o _ sin que se tomen como comodines.
        comando.CommandText = $"""
            {SelectProductos}
            WHERE Nombre LIKE $patron ESCAPE '\' OR CodigoBarras LIKE $patron ESCAPE '\'
            ORDER BY Nombre COLLATE NOCASE
            """;
        comando.Parameters.AddWithValue("$patron", $"%{EscaparLike(texto.Trim())}%");

        var productos = new List<Producto>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            productos.Add(Leer(lector));
        return productos;
    }

    public void Agregar(Producto producto)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            INSERT INTO Productos (CodigoBarras, Nombre, PrecioCostoCentavos, PrecioVentaCentavos, Stock, StockMinimo)
            VALUES ($codigo, $nombre, $costo, $venta, $stock, $minimo)
            RETURNING Id
            """;
        CargarParametros(comando, producto);
        producto.AsignarId((long)comando.ExecuteScalar()!);
    }

    public void Actualizar(Producto producto) => ActualizarVarios([producto]);

    public void ActualizarVarios(IReadOnlyCollection<Producto> productos)
    {
        using var db = conexion.Abrir();
        using var transaccion = db.BeginTransaction();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            UPDATE Productos
            SET CodigoBarras = $codigo, Nombre = $nombre, PrecioCostoCentavos = $costo,
                PrecioVentaCentavos = $venta, Stock = $stock, StockMinimo = $minimo
            WHERE Id = $id
            """;

        foreach (var producto in productos)
        {
            comando.Parameters.Clear();
            CargarParametros(comando, producto);
            comando.Parameters.AddWithValue("$id", producto.Id);
            comando.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public void Eliminar(long id)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = "DELETE FROM Productos WHERE Id = $id";
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public bool TieneVentas(long id)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = "SELECT EXISTS (SELECT 1 FROM VentaLineas WHERE ProductoId = $id)";
        comando.Parameters.AddWithValue("$id", id);
        return (long)comando.ExecuteScalar()! == 1;
    }

    private Producto? ConsultarUno(string sql, (string Nombre, object Valor) parametro)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue(parametro.Nombre, parametro.Valor);

        using var lector = comando.ExecuteReader();
        return lector.Read() ? Leer(lector) : null;
    }

    private static void CargarParametros(SqliteCommand comando, Producto producto)
    {
        comando.Parameters.AddWithValue("$codigo", producto.CodigoBarras);
        comando.Parameters.AddWithValue("$nombre", producto.Nombre);
        comando.Parameters.AddWithValue("$costo", ACentavos(producto.PrecioCosto));
        comando.Parameters.AddWithValue("$venta", ACentavos(producto.PrecioVenta));
        comando.Parameters.AddWithValue("$stock", producto.Stock);
        comando.Parameters.AddWithValue("$minimo", producto.StockMinimo);
    }

    private static Producto Leer(SqliteDataReader lector) => Producto.Restaurar(
        id: lector.GetInt64(0),
        codigoBarras: lector.GetString(1),
        nombre: lector.GetString(2),
        precioCosto: DeCentavos(lector.GetInt64(3)),
        precioVenta: DeCentavos(lector.GetInt64(4)),
        stock: lector.GetInt32(5),
        stockMinimo: lector.GetInt32(6));

    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
