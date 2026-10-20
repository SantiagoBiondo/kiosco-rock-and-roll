namespace KioscoRockAndRoll.Dominio.Entidades;

public enum PreciosAAjustar
{
    CostoYVenta = 1,
    SoloVenta = 2,
    SoloCosto = 3
}

public static class PreciosAAjustarExtensiones
{
    public static string Descripcion(this PreciosAAjustar precios) => precios switch
    {
        PreciosAAjustar.CostoYVenta => "Costo y venta",
        PreciosAAjustar.SoloVenta => "Solo precio de venta",
        PreciosAAjustar.SoloCosto => "Solo precio de costo",
        _ => precios.ToString()
    };
}

/// <summary>
/// Un aumento (o descuento) porcentual de precios, por ejemplo "+15% a costo y venta,
/// redondeando la venta hacia arriba a múltiplos de $10".
/// </summary>
public sealed class AjustePrecios
{
    public const decimal PorcentajeMaximo = 1000;

    /// <param name="porcentaje">Positivo para aumentar, negativo para bajar. 15 = +15%.</param>
    /// <param name="redondearVentaA">
    /// Si es mayor a cero, el precio de venta se redondea hacia arriba a múltiplos de este valor
    /// (por ejemplo 10: $1.234 pasa a $1.240). El costo nunca se redondea.
    /// </param>
    public AjustePrecios(decimal porcentaje, PreciosAAjustar precios, decimal redondearVentaA = 0)
    {
        if (porcentaje == 0)
            throw new ReglaDeNegocioException("Ingresá un porcentaje distinto de cero.");
        if (porcentaje <= -100)
            throw new ReglaDeNegocioException("No se puede bajar un 100% o más.");
        if (porcentaje > PorcentajeMaximo)
            throw new ReglaDeNegocioException($"El aumento no puede superar el {PorcentajeMaximo}%.");
        if (!Enum.IsDefined(precios))
            throw new ArgumentOutOfRangeException(nameof(precios));
        if (redondearVentaA < 0)
            throw new ArgumentOutOfRangeException(nameof(redondearVentaA));

        Porcentaje = porcentaje;
        Precios = precios;
        RedondearVentaA = redondearVentaA;
    }

    public decimal Porcentaje { get; }
    public PreciosAAjustar Precios { get; }
    public decimal RedondearVentaA { get; }

    public bool AjustaCosto => Precios != PreciosAAjustar.SoloVenta;
    public bool AjustaVenta => Precios != PreciosAAjustar.SoloCosto;

    public decimal NuevoCosto(decimal costo) =>
        AjustaCosto ? decimal.Round(Aumentar(costo), 2, MidpointRounding.AwayFromZero) : costo;

    public decimal NuevaVenta(decimal venta)
    {
        if (!AjustaVenta)
            return venta;

        var nueva = Aumentar(venta);
        return RedondearVentaA > 0
            ? Math.Ceiling(nueva / RedondearVentaA) * RedondearVentaA
            : decimal.Round(nueva, 2, MidpointRounding.AwayFromZero);
    }

    private decimal Aumentar(decimal precio) => precio * (1 + Porcentaje / 100);
}

/// <summary>Cómo quedaría un producto después de un ajuste (para mostrar antes de aplicarlo).</summary>
public sealed record CambioDePrecio(
    long ProductoId, string Nombre,
    decimal CostoAntes, decimal CostoDespues,
    decimal VentaAntes, decimal VentaDespues);
