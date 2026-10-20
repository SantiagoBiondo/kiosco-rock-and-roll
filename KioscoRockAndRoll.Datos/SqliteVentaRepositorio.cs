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
            INSERT INTO VentaLineas (VentaId, ProductoId, NombreProducto, Cantidad, PrecioUnitarioCentavos, CostoUnitarioCentavos)
            VALUES ($venta, $producto, $nombre, $cantidad, $precio, $costo);
            UPDATE Productos SET Stock = Stock - $cantidad WHERE Id = $producto;
            """;
        var pVenta = comandoLinea.Parameters.Add("$venta", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pProducto = comandoLinea.Parameters.Add("$producto", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pNombre = comandoLinea.Parameters.Add("$nombre", Microsoft.Data.Sqlite.SqliteType.Text);
        var pCantidad = comandoLinea.Parameters.Add("$cantidad", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pPrecio = comandoLinea.Parameters.Add("$precio", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pCosto = comandoLinea.Parameters.Add("$costo", Microsoft.Data.Sqlite.SqliteType.Integer);

        foreach (var linea in venta.Lineas)
        {
            pVenta.Value = venta.Id;
            pProducto.Value = linea.ProductoId;
            pNombre.Value = linea.NombreProducto;
            pCantidad.Value = linea.Cantidad;
            pPrecio.Value = ACentavos(linea.PrecioUnitario);
            pCosto.Value = ACentavos(linea.CostoUnitario);
            comandoLinea.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    private const string SelectResumenes = """
        SELECT v.Id, v.Fecha, v.MedioPago,
               COALESCE(SUM(l.Cantidad), 0),
               COALESCE(SUM(l.Cantidad * l.PrecioUnitarioCentavos), 0),
               v.TurnoId, v.AnuladaEn, v.MotivoAnulacion
        FROM Ventas v
        LEFT JOIN VentaLineas l ON l.VentaId = v.Id
        """;

    public IReadOnlyList<ResumenVenta> ListarDelDia(DateOnly dia)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"""
            {SelectResumenes}
            WHERE v.Fecha >= $desde AND v.Fecha < $hasta
            GROUP BY v.Id
            ORDER BY v.Fecha
            """;
        comando.Parameters.AddWithValue("$desde", AFechaTexto(dia.ToDateTime(TimeOnly.MinValue)));
        comando.Parameters.AddWithValue("$hasta", AFechaTexto(dia.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        var ventas = new List<ResumenVenta>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            ventas.Add(LeerResumen(lector));
        return ventas;
    }

    public IReadOnlyList<ProductoVendido> ProductosVendidosDelDia(DateOnly dia)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        // Se agrupa por producto; el nombre se toma de las ventas (es el que tenía al venderse).
        comando.CommandText = """
            SELECT MAX(l.NombreProducto), SUM(l.Cantidad), SUM(l.Cantidad * l.PrecioUnitarioCentavos) AS Total
            FROM VentaLineas l
            JOIN Ventas v ON v.Id = l.VentaId
            WHERE v.Fecha >= $desde AND v.Fecha < $hasta AND v.AnuladaEn IS NULL
            GROUP BY l.ProductoId
            ORDER BY Total DESC
            """;
        comando.Parameters.AddWithValue("$desde", AFechaTexto(dia.ToDateTime(TimeOnly.MinValue)));
        comando.Parameters.AddWithValue("$hasta", AFechaTexto(dia.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        var productos = new List<ProductoVendido>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            productos.Add(new ProductoVendido(lector.GetString(0), lector.GetInt32(1), DeCentavos(lector.GetInt64(2))));
        return productos;
    }

    public IReadOnlyList<VentasDeUnDia> TotalesPorDia(PeriodoReporte periodo)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        // Las fechas se guardan como "yyyy-MM-dd HH:mm:ss": los primeros 10 caracteres son el día.
        comando.CommandText = """
            SELECT substr(v.Fecha, 1, 10) AS Dia,
                   COUNT(DISTINCT v.Id),
                   SUM(l.Cantidad * l.PrecioUnitarioCentavos),
                   SUM(l.Cantidad * l.CostoUnitarioCentavos)
            FROM Ventas v
            JOIN VentaLineas l ON l.VentaId = v.Id
            WHERE v.Fecha >= $desde AND v.Fecha < $hasta AND v.AnuladaEn IS NULL
            GROUP BY Dia
            ORDER BY Dia
            """;
        CargarPeriodo(comando, periodo);

        var dias = new List<VentasDeUnDia>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            dias.Add(new VentasDeUnDia(
                DateOnly.ParseExact(lector.GetString(0), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                lector.GetInt32(1), DeCentavos(lector.GetInt64(2)), DeCentavos(lector.GetInt64(3))));
        }
        return dias;
    }

    public IReadOnlyList<RendimientoProducto> RendimientoPorProducto(PeriodoReporte periodo)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            SELECT l.ProductoId, MAX(l.NombreProducto), SUM(l.Cantidad),
                   SUM(l.Cantidad * l.PrecioUnitarioCentavos),
                   SUM(l.Cantidad * l.CostoUnitarioCentavos)
            FROM VentaLineas l
            JOIN Ventas v ON v.Id = l.VentaId
            WHERE v.Fecha >= $desde AND v.Fecha < $hasta AND v.AnuladaEn IS NULL
            GROUP BY l.ProductoId
            """;
        CargarPeriodo(comando, periodo);

        var productos = new List<RendimientoProducto>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            productos.Add(new RendimientoProducto(
                lector.GetInt64(0), lector.GetString(1), lector.GetInt32(2),
                DeCentavos(lector.GetInt64(3)), DeCentavos(lector.GetInt64(4))));
        }
        return productos;
    }

    private static void CargarPeriodo(Microsoft.Data.Sqlite.SqliteCommand comando, PeriodoReporte periodo)
    {
        comando.Parameters.AddWithValue("$desde", AFechaTexto(periodo.Desde.ToDateTime(TimeOnly.MinValue)));
        comando.Parameters.AddWithValue("$hasta", AFechaTexto(periodo.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue)));
    }

    public DetalleVenta? ObtenerDetalle(long ventaId)
    {
        using var db = conexion.Abrir();
        ResumenVenta resumen;
        using (var comando = db.CreateCommand())
        {
            comando.CommandText = $"{SelectResumenes} WHERE v.Id = $id GROUP BY v.Id";
            comando.Parameters.AddWithValue("$id", ventaId);
            using var lector = comando.ExecuteReader();
            if (!lector.Read())
                return null;
            resumen = LeerResumen(lector);
        }

        using var comandoLineas = db.CreateCommand();
        comandoLineas.CommandText = """
            SELECT ProductoId, NombreProducto, Cantidad, PrecioUnitarioCentavos, CostoUnitarioCentavos
            FROM VentaLineas WHERE VentaId = $id ORDER BY Id
            """;
        comandoLineas.Parameters.AddWithValue("$id", ventaId);

        var lineas = new List<LineaVenta>();
        using var lectorLineas = comandoLineas.ExecuteReader();
        while (lectorLineas.Read())
        {
            lineas.Add(new LineaVenta(
                lectorLineas.GetInt64(0), lectorLineas.GetString(1), lectorLineas.GetInt32(2),
                DeCentavos(lectorLineas.GetInt64(3)), DeCentavos(lectorLineas.GetInt64(4))));
        }
        return new DetalleVenta(resumen, lineas);
    }

    public void Anular(long ventaId, string motivo, DateTime fecha)
    {
        using var db = conexion.Abrir();
        using var transaccion = db.BeginTransaction();
        using var comando = db.CreateCommand();

        // "AnuladaEn IS NULL" evita anular dos veces aunque se llegue acá por dos caminos a la vez.
        comando.CommandText = """
            UPDATE Ventas SET AnuladaEn = $fecha, MotivoAnulacion = $motivo
            WHERE Id = $id AND AnuladaEn IS NULL
            """;
        comando.Parameters.AddWithValue("$fecha", AFechaTexto(fecha));
        comando.Parameters.AddWithValue("$motivo", motivo);
        comando.Parameters.AddWithValue("$id", ventaId);
        if (comando.ExecuteNonQuery() != 1)
            throw new InvalidOperationException($"La venta {ventaId} no existe o ya estaba anulada.");

        comando.CommandText = """
            UPDATE Productos
            SET Stock = Stock + (SELECT SUM(l.Cantidad) FROM VentaLineas l
                                 WHERE l.VentaId = $id AND l.ProductoId = Productos.Id)
            WHERE Id IN (SELECT ProductoId FROM VentaLineas WHERE VentaId = $id)
            """;
        comando.ExecuteNonQuery();

        transaccion.Commit();
    }

    private static ResumenVenta LeerResumen(Microsoft.Data.Sqlite.SqliteDataReader lector) => new(
        Id: lector.GetInt64(0),
        Fecha: DeFechaTexto(lector.GetString(1)),
        MedioDePago: (MedioDePago)lector.GetInt32(2),
        Unidades: lector.GetInt32(3),
        Total: DeCentavos(lector.GetInt64(4)),
        TurnoId: lector.IsDBNull(5) ? null : lector.GetInt64(5),
        AnuladaEn: lector.IsDBNull(6) ? null : DeFechaTexto(lector.GetString(6)),
        MotivoAnulacion: lector.IsDBNull(7) ? null : lector.GetString(7));
}
