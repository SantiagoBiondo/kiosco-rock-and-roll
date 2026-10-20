using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;
using Microsoft.Data.Sqlite;
using static KioscoRockAndRoll.Datos.Conversiones;

namespace KioscoRockAndRoll.Datos;

public sealed class SqliteCajaRepositorio(ConexionSqlite conexion) : ICajaRepositorio
{
    private const string SelectTurnos =
        "SELECT Id, Apertura, EfectivoInicialCentavos, Cierre, EfectivoContadoCentavos FROM Turnos";

    public Turno? ObtenerAbierto() =>
        ListarTurnos($"{SelectTurnos} WHERE Cierre IS NULL", cantidad: null).FirstOrDefault();

    public IReadOnlyList<Turno> ListarRecientes(int cantidad) =>
        ListarTurnos($"{SelectTurnos} ORDER BY Id DESC LIMIT $cantidad", cantidad);

    public IReadOnlyList<Turno> ListarDelDia(DateOnly dia)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"{SelectTurnos} WHERE Apertura >= $desde AND Apertura < $hasta ORDER BY Id";
        comando.Parameters.AddWithValue("$desde", AFechaTexto(dia.ToDateTime(TimeOnly.MinValue)));
        comando.Parameters.AddWithValue("$hasta", AFechaTexto(dia.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        var turnos = new List<Turno>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            turnos.Add(Leer(lector));
        return turnos;
    }

    public void Agregar(Turno turno)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            INSERT INTO Turnos (Apertura, EfectivoInicialCentavos) VALUES ($apertura, $inicial)
            RETURNING Id
            """;
        comando.Parameters.AddWithValue("$apertura", AFechaTexto(turno.Apertura));
        comando.Parameters.AddWithValue("$inicial", ACentavos(turno.EfectivoInicial));
        turno.AsignarId((long)comando.ExecuteScalar()!);
    }

    public void GuardarCierre(Turno turno)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            UPDATE Turnos SET Cierre = $cierre, EfectivoContadoCentavos = $contado
            WHERE Id = $id
            """;
        comando.Parameters.AddWithValue("$cierre", turno.Cierre is { } cierre ? AFechaTexto(cierre) : DBNull.Value);
        comando.Parameters.AddWithValue("$contado", turno.EfectivoContado is { } contado ? ACentavos(contado) : DBNull.Value);
        comando.Parameters.AddWithValue("$id", turno.Id);
        comando.ExecuteNonQuery();
    }

    public void AgregarMovimiento(long turnoId, MovimientoCaja movimiento)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            INSERT INTO MovimientosCaja (TurnoId, Fecha, Tipo, MontoCentavos, Motivo)
            VALUES ($turno, $fecha, $tipo, $monto, $motivo)
            RETURNING Id
            """;
        comando.Parameters.AddWithValue("$turno", turnoId);
        comando.Parameters.AddWithValue("$fecha", AFechaTexto(movimiento.Fecha));
        comando.Parameters.AddWithValue("$tipo", (int)movimiento.Tipo);
        comando.Parameters.AddWithValue("$monto", ACentavos(movimiento.Monto));
        comando.Parameters.AddWithValue("$motivo", movimiento.Motivo);
        movimiento.AsignarId((long)comando.ExecuteScalar()!);
    }

    public IReadOnlyList<MovimientoCaja> ListarMovimientos(long turnoId)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = """
            SELECT Id, Fecha, Tipo, MontoCentavos, Motivo FROM MovimientosCaja
            WHERE TurnoId = $turno ORDER BY Id
            """;
        comando.Parameters.AddWithValue("$turno", turnoId);

        var movimientos = new List<MovimientoCaja>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            movimientos.Add(MovimientoCaja.Restaurar(
                lector.GetInt64(0), DeFechaTexto(lector.GetString(1)), (TipoMovimientoCaja)lector.GetInt32(2),
                DeCentavos(lector.GetInt64(3)), lector.GetString(4)));
        }
        return movimientos;
    }

    public IReadOnlyList<TotalPorMedio> TotalesDeVentas(long turnoId)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        // Se suma desde los pagos: una venta mixta aporta cada parte a su medio.
        comando.CommandText = """
            SELECT p.Medio, COUNT(DISTINCT v.Id), SUM(p.MontoCentavos)
            FROM VentaPagos p
            JOIN Ventas v ON v.Id = p.VentaId
            WHERE v.TurnoId = $turno AND v.AnuladaEn IS NULL
            GROUP BY p.Medio
            ORDER BY p.Medio
            """;
        comando.Parameters.AddWithValue("$turno", turnoId);

        var totales = new List<TotalPorMedio>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            totales.Add(new TotalPorMedio((MedioDePago)lector.GetInt32(0), lector.GetInt32(1), DeCentavos(lector.GetInt64(2))));
        return totales;
    }

    public int CantidadDeVentas(long turnoId)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM Ventas WHERE TurnoId = $turno AND AnuladaEn IS NULL";
        comando.Parameters.AddWithValue("$turno", turnoId);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    private List<Turno> ListarTurnos(string sql, int? cantidad)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        if (cantidad is { } n)
            comando.Parameters.AddWithValue("$cantidad", n);

        var turnos = new List<Turno>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            turnos.Add(Leer(lector));
        return turnos;
    }

    private static Turno Leer(SqliteDataReader lector) => Turno.Restaurar(
        id: lector.GetInt64(0),
        apertura: DeFechaTexto(lector.GetString(1)),
        efectivoInicial: DeCentavos(lector.GetInt64(2)),
        cierre: lector.IsDBNull(3) ? null : DeFechaTexto(lector.GetString(3)),
        efectivoContado: lector.IsDBNull(4) ? null : DeCentavos(lector.GetInt64(4)));
}
