using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Apertura, movimientos y cierre de caja.</summary>
public class CajaServicio(ICajaRepositorio caja, TimeProvider reloj)
{
    private DateTime Ahora => reloj.GetLocalNow().DateTime;

    /// <summary>El turno abierto, o null si la caja está cerrada.</summary>
    public Turno? TurnoAbierto => caja.ObtenerAbierto();

    public Turno AbrirCaja(decimal efectivoInicial)
    {
        if (TurnoAbierto != null)
            throw new ReglaDeNegocioException("La caja ya está abierta.");

        var turno = Turno.Abrir(efectivoInicial, Ahora);
        caja.Agregar(turno);
        return turno;
    }

    public MovimientoCaja RegistrarMovimiento(TipoMovimientoCaja tipo, decimal monto, string motivo)
    {
        var turno = TurnoAbierto ?? throw new ReglaDeNegocioException("La caja está cerrada.");
        var movimiento = MovimientoCaja.Crear(tipo, monto, motivo, Ahora);
        caja.AgregarMovimiento(turno.Id, movimiento);
        return movimiento;
    }

    public ResumenTurno Resumen(Turno turno) =>
        new(turno, caja.TotalesDeVentas(turno.Id), caja.ListarMovimientos(turno.Id));

    /// <summary>Cierra la caja con el efectivo contado y devuelve las cuentas finales.</summary>
    public ResumenTurno CerrarCaja(decimal efectivoContado)
    {
        var turno = TurnoAbierto ?? throw new ReglaDeNegocioException("La caja ya está cerrada.");
        turno.Cerrar(efectivoContado, Ahora);
        caja.GuardarCierre(turno);
        return Resumen(turno);
    }

    /// <summary>Los turnos abiertos ese día (cerrados o no), en orden.</summary>
    public IReadOnlyList<ResumenTurno> TurnosDelDia(DateOnly dia) =>
        caja.ListarDelDia(dia).Select(Resumen).ToList();

    /// <summary>Los últimos turnos cerrados, del más nuevo al más viejo.</summary>
    public IReadOnlyList<ResumenTurno> TurnosAnteriores(int cantidad = 60) =>
        caja.ListarRecientes(cantidad)
            .Where(t => !t.EstaAbierto)
            .Select(Resumen)
            .ToList();
}
