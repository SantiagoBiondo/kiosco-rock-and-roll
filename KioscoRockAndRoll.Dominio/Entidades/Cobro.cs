namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Una parte de un cobro: cuánto se pagó con un medio.</summary>
public sealed record Pago(MedioDePago Medio, decimal Monto);

/// <summary>
/// Cómo se paga una venta: con un solo medio o combinando varios (por ejemplo, una parte con débito
/// y el resto en efectivo). Lo que se paga en efectivo puede dar vuelto.
/// </summary>
public sealed class Cobro
{
    private Cobro(decimal total, IReadOnlyList<Pago> pagos, decimal efectivoRecibido)
    {
        Total = total;
        Pagos = pagos;
        EfectivoRecibido = efectivoRecibido;
    }

    public decimal Total { get; }

    /// <summary>Lo que se aplica a la venta con cada medio (suma exactamente el total). Un renglón por medio.</summary>
    public IReadOnlyList<Pago> Pagos { get; }

    /// <summary>La parte del total que se paga en efectivo.</summary>
    public decimal EfectivoACobrar => Pagos.Where(p => p.Medio == MedioDePago.Efectivo).Sum(p => p.Monto);

    /// <summary>El efectivo que entregó el cliente (puede ser más que lo que se cobra en efectivo).</summary>
    public decimal EfectivoRecibido { get; }

    public decimal Vuelto => EfectivoRecibido - EfectivoACobrar;

    public bool EsMixto => Pagos.Count > 1;

    /// <summary>El medio con el que se pagó la mayor parte.</summary>
    public MedioDePago MedioPrincipal => Pagos.MaxBy(p => p.Monto)!.Medio;

    /// <summary>"Débito" o, si es mixto, "Débito $ 1.000,00 + Efectivo $ 500,00".</summary>
    public string Descripcion => DescribirPagos(Pagos);

    /// <summary>Cobro con un solo medio.</summary>
    /// <param name="montoRecibido">Solo cuenta para efectivo. Si es null o 0, se asume que pagó justo.</param>
    public static Cobro Crear(decimal total, MedioDePago medio, decimal? montoRecibido = null) =>
        medio == MedioDePago.Efectivo
            ? Combinado(total, [], montoRecibido)
            : Combinado(total, [new Pago(medio, total)]);

    /// <summary>
    /// Cobro combinando medios: <paramref name="otrosMedios"/> es lo pagado con tarjeta o transferencia,
    /// y lo que falte para llegar al total se paga en efectivo.
    /// </summary>
    /// <param name="efectivoRecibido">
    /// El efectivo que entrega el cliente, para calcular el vuelto. Si es null o 0, se asume que pagó justo.
    /// </param>
    public static Cobro Combinado(decimal total, IEnumerable<Pago> otrosMedios, decimal? efectivoRecibido = null)
    {
        if (total <= 0)
            throw new ReglaDeNegocioException("No hay nada para cobrar.");

        var otros = otrosMedios.Where(p => p.Monto != 0).ToList();
        if (otros.Any(p => p.Monto < 0))
            throw new ReglaDeNegocioException("Los montos no pueden ser negativos.");
        if (otros.Any(p => p.Medio == MedioDePago.Efectivo || !Enum.IsDefined(p.Medio)))
            throw new ArgumentException("El efectivo no va en los otros medios: es lo que falta para llegar al total.", nameof(otrosMedios));
        if (otros.GroupBy(p => p.Medio).Any(g => g.Count() > 1))
            throw new ArgumentException("Cada medio va una sola vez.", nameof(otrosMedios));

        var sumaOtros = otros.Sum(p => p.Monto);
        if (sumaOtros > total)
            throw new ReglaDeNegocioException(
                $"Con tarjeta o transferencia se están cobrando {sumaOtros:C2}, más que el total ({total:C2}).");

        var efectivo = total - sumaOtros;
        var pagos = efectivo > 0 ? [.. otros, new Pago(MedioDePago.Efectivo, efectivo)] : otros;

        decimal recibido = 0;
        if (efectivo > 0)
        {
            recibido = efectivoRecibido is null or 0 ? efectivo : efectivoRecibido.Value;
            if (recibido < efectivo)
                throw new ReglaDeNegocioException($"Faltan {efectivo - recibido:C2} en efectivo.");
        }

        return new Cobro(total, pagos.OrderBy(p => p.Medio).ToList(), recibido);
    }

    public static string DescribirPagos(IReadOnlyList<Pago> pagos) => pagos.Count == 1
        ? pagos[0].Medio.Descripcion()
        : string.Join(" + ", pagos.Select(p => $"{p.Medio.Descripcion()} {p.Monto:C2}"));
}
