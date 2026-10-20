namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Cómo se paga una venta. Si es en efectivo, calcula el vuelto.
/// </summary>
public sealed class Cobro
{
    private Cobro(decimal total, MedioDePago medio, decimal montoRecibido)
    {
        Total = total;
        Medio = medio;
        MontoRecibido = montoRecibido;
    }

    public decimal Total { get; }
    public MedioDePago Medio { get; }
    public decimal MontoRecibido { get; }
    public decimal Vuelto => MontoRecibido - Total;

    /// <param name="montoRecibido">
    /// Solo cuenta para efectivo. Si es null o 0, se asume que pagó justo.
    /// </param>
    public static Cobro Crear(decimal total, MedioDePago medio, decimal? montoRecibido = null)
    {
        if (total <= 0)
            throw new ReglaDeNegocioException("No hay nada para cobrar.");
        if (!Enum.IsDefined(medio))
            throw new ArgumentOutOfRangeException(nameof(medio));

        // Con tarjeta o transferencia se cobra el monto exacto.
        if (medio != MedioDePago.Efectivo || montoRecibido is null or 0)
            return new Cobro(total, medio, total);

        if (montoRecibido < total)
            throw new ReglaDeNegocioException($"Faltan {total - montoRecibido.Value:C2} para cubrir el total.");

        return new Cobro(total, medio, montoRecibido.Value);
    }
}
