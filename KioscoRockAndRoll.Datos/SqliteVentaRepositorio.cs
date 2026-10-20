using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;
using static KioscoRockAndRoll.Datos.Conversiones;

namespace KioscoRockAndRoll.Datos;

public sealed class SqliteVentaRepositorio(ConexionSqlite conexion) : IVentaRepositorio
{
    public void Registrar(Venta venta)
    {
        using var db = conexion.Abrir();
        using var transaccion = db.BeginTransaction();

        using (var comandoVenta = db.CreateCommand())
        {
            comandoVenta.CommandText = """
                INSERT INTO Ventas (Fecha, MedioPago, TurnoId) VALUES ($fecha, $medio, $turno) RETURNING Id
                """;
            comandoVenta.Parameters.AddWithValue("$fecha", AFechaTexto(venta.Fecha));
            comandoVenta.Parameters.AddWithValue("$turno", venta.TurnoId);
            comandoVenta.Parameters.AddWithValue("$medio", (int)venta.MedioDePago);
            venta.AsignarId((long)comandoVenta.ExecuteScalar()!);
        }

        using var comandoLinea = db.CreateCommand();
        comandoLinea.CommandText = """
            INSERT INTO VentaLineas (VentaId, ProductoId, NombreProducto, Cantidad, PrecioUnitarioCentavos)
            VALUES ($venta, $producto, $nombre, $cantidad, $precio);
            UPDATE Productos SET Stock = Stock - $cantidad WHERE Id = $producto;
            """;
        var pVenta = comandoLinea.Parameters.Add("$venta", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pProducto = comandoLinea.Parameters.Add("$producto", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pNombre = comandoLinea.Parameters.Add("$nombre", Microsoft.Data.Sqlite.SqliteType.Text);
        var pCantidad = comandoLinea.Parameters.Add("$cantidad", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pPrecio = comandoLinea.Parameters.Add("$precio", Microsoft.Data.Sqlite.SqliteType.Integer);

        foreach (var linea in venta.Lineas)
        {
            pVenta.Value = venta.Id;
            pProducto.Value = linea.ProductoId;
            pNombre.Value = linea.NombreProducto;
            pCantidad.Value = linea.Cantidad;
            pPrecio.Value = ACentavos(linea.PrecioUnitario);
            comandoLinea.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public IReadOnlyList<ResumenVenta> ListarDelDia(DateOnly dia)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            SELECT v.Id, v.Fecha, v.MedioPago,
                   COALESCE(SUM(l.Cantidad), 0),
                   COALESCE(SUM(l.Cantidad * l.PrecioUnitarioCentavos), 0)
            FROM Ventas v
            LEFT JOIN VentaLineas l ON l.VentaId = v.Id
            WHERE v.Fecha >= $desde AND v.Fecha < $hasta
            GROUP BY v.Id
            ORDER BY v.Fecha
            """;
        comando.Parameters.AddWithValue("$desde", AFechaTexto(dia.ToDateTime(TimeOnly.MinValue)));
        comando.Parameters.AddWithValue("$hasta", AFechaTexto(dia.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        var ventas = new List<ResumenVenta>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            ventas.Add(new ResumenVenta(
                Id: lector.GetInt64(0),
                Fecha: DeFechaTexto(lector.GetString(1)),
                MedioDePago: (MedioDePago)lector.GetInt32(2),
                Unidades: lector.GetInt32(3),
                Total: DeCentavos(lector.GetInt64(4))));
        }
        return ventas;
    }
}
