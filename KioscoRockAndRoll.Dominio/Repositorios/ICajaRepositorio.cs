using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Repositorios;

/// <summary>Dónde se guardan los turnos de caja y sus movimientos.</summary>
public interface ICajaRepositorio
{
    /// <summary>El turno abierto, o null si la caja está cerrada.</summary>
    Turno? ObtenerAbierto();

    /// <summary>Los últimos turnos, del más nuevo al más viejo.</summary>
    IReadOnlyList<Turno> ListarRecientes(int cantidad);

    /// <summary>Los turnos que se abrieron ese día, en orden.</summary>
    IReadOnlyList<Turno> ListarDelDia(DateOnly dia);

    void Agregar(Turno turno);
    void GuardarCierre(Turno turno);

    void AgregarMovimiento(long turnoId, MovimientoCaja movimiento);
    IReadOnlyList<MovimientoCaja> ListarMovimientos(long turnoId);

    /// <summary>Lo vendido en el turno, agrupado por medio de pago.</summary>
    IReadOnlyList<TotalPorMedio> TotalesDeVentas(long turnoId);
}
