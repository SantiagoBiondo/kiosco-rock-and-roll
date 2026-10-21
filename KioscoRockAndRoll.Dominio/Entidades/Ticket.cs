using System.Diagnostics.CodeAnalysis;

namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Un renglón del ticket.</summary>
public sealed record LineaTicket(int Cantidad, string Descripcion, decimal PrecioUnitario)
{
    public decimal Importe => Cantidad * PrecioUnitario;
}

/// <summary>
/// El ticket (no fiscal) de una venta: lo que se imprime para el cliente.
/// Se arma al cobrar (con el vuelto) o después, como copia de una venta ya hecha.
/// </summary>
public sealed record Ticket(
    string Negocio,
    long NumeroVenta,
    DateTime Fecha,
    IReadOnlyList<LineaTicket> Lineas,
    IReadOnlyList<Pago> Pagos,
    decimal? EfectivoRecibido,
    bool EsCopia)
{
    public decimal Total => Lineas.Sum(l => l.Importe);

    /// <summary>Vuelto, solo si se pagó con efectivo y el cliente entregó más de lo que debía.</summary>
    public decimal? Vuelto
    {
        get
        {
            var efectivo = Pagos.Where(p => p.Medio == MedioDePago.Efectivo).Sum(p => p.Monto);
            return EfectivoRecibido is { } recibido && recibido > efectivo ? recibido - efectivo : null;
        }
    }

    /// <summary>El ticket de una venta recién cobrada.</summary>
    public static Ticket DeVenta(Venta venta, Cobro cobro, string negocio) => new(
        negocio, venta.Id, venta.Fecha,
        venta.Lineas.Select(l => new LineaTicket(l.Cantidad, l.NombreProducto, l.PrecioUnitario)).ToList(),
        cobro.Pagos,
        cobro.EfectivoACobrar > 0 ? cobro.EfectivoRecibido : null,
        EsCopia: false);

    /// <summary>Una copia del ticket de una venta ya hecha (no se sabe con cuánto pagó, así que no lleva vuelto).</summary>
    public static Ticket CopiaDe(DetalleVenta detalle, string negocio)
    {
        if (detalle.Venta.Anulada)
            throw new ReglaDeNegocioException("La venta está anulada: no se imprime su ticket.");

        return new(
            negocio, detalle.Venta.Id, detalle.Venta.Fecha,
            detalle.Lineas.Select(l => new LineaTicket(l.Cantidad, l.NombreProducto, l.PrecioUnitario)).ToList(),
            detalle.Venta.PagosRealizados,
            EfectivoRecibido: null,
            EsCopia: true);
    }
}

public enum AnchoDePapel
{
    /// <summary>Rollo de 58 mm: 32 caracteres por renglón.</summary>
    Rollo58mm = 58,

    /// <summary>Rollo de 80 mm: 48 caracteres por renglón.</summary>
    Rollo80mm = 80
}

/// <summary>Cómo se imprimen los tickets en este kiosco.</summary>
/// <param name="Impresora">Nombre de la impresora en Windows, o null si no hay ninguna elegida.</param>
/// <param name="ImprimirAlCobrar">Si al cobrar la casilla "Imprimir ticket" viene marcada.</param>
public sealed record ConfiguracionDeTicket(string? Impresora, AnchoDePapel Ancho, bool ImprimirAlCobrar)
{
    public static ConfiguracionDeTicket PorDefecto { get; } = new(null, AnchoDePapel.Rollo58mm, ImprimirAlCobrar: false);

    [MemberNotNullWhen(true, nameof(Impresora))]
    public bool HayImpresora => !string.IsNullOrWhiteSpace(Impresora);

    /// <summary>Caracteres por renglón (los estándar de las impresoras de tickets).</summary>
    public int Columnas => Ancho == AnchoDePapel.Rollo80mm ? 48 : 32;

    /// <summary>Ancho imprimible del papel (el rollo tiene unos milímetros de margen a cada lado).</summary>
    public decimal AnchoImprimibleMm => Ancho == AnchoDePapel.Rollo80mm ? 72 : 48;
}
