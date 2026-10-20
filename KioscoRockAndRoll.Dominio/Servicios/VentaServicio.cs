using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Totales de un día, separados por medio de pago.</summary>
public sealed record ResumenDelDia(
    DateOnly Dia,
    IReadOnlyList<ResumenVenta> Ventas,
    IReadOnlyDictionary<MedioDePago, decimal> TotalPorMedio)
{
    public decimal Total => Ventas.Sum(v => v.Total);
}

/// <summary>Casos de uso de la venta: cobrar y consultar lo vendido.</summary>
public class VentaServicio(IVentaRepositorio ventas, CajaServicio caja, TimeProvider reloj)
{
    /// <summary>
    /// Registra la venta en el turno de caja abierto, descuenta el stock y vacía el carrito.
    /// Si la caja está cerrada, no se puede cobrar.
    /// </summary>
    public Venta Cobrar(Carrito carrito, Cobro cobro)
    {
        var turno = caja.TurnoAbierto
                    ?? throw new ReglaDeNegocioException("La caja está cerrada. Abrila para poder cobrar.");
        var venta = Venta.DesdeCarrito(carrito, cobro, turno, reloj.GetLocalNow().DateTime);
        ventas.Registrar(venta);

        // El repositorio ya descontó el stock en la base; reflejamos lo mismo en memoria.
        foreach (var item in carrito.Items)
            item.Producto.DescontarStock(item.Cantidad);

        carrito.Vaciar();
        return venta;
    }

    public ResumenDelDia ResumenDelDia(DateOnly dia)
    {
        var delDia = ventas.ListarDelDia(dia);
        var porMedio = delDia
            .GroupBy(v => v.MedioDePago)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Total));
        return new ResumenDelDia(dia, delDia, porMedio);
    }
}
