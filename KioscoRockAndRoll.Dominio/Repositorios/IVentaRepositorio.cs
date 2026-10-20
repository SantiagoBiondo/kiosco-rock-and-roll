using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Repositorios;

public interface IVentaRepositorio
{
    /// <summary>
    /// Guarda la venta y descuenta el stock de cada producto en una sola operación atómica:
    /// o se guarda todo, o no se guarda nada.
    /// </summary>
    void Registrar(Venta venta);

    /// <summary>Las ventas del día, incluidas las anuladas.</summary>
    IReadOnlyList<ResumenVenta> ListarDelDia(DateOnly dia);

    /// <summary>Unidades y recaudación por producto en el día, sin ventas anuladas, de mayor a menor recaudación.</summary>
    IReadOnlyList<ProductoVendido> ProductosVendidosDelDia(DateOnly dia);

    /// <summary>Ventas, vendido y costo de cada día del período que tuvo ventas (sin anuladas).</summary>
    IReadOnlyList<VentasDeUnDia> TotalesPorDia(PeriodoReporte periodo);

    /// <summary>Unidades, vendido y costo de cada producto en el período (sin anuladas).</summary>
    IReadOnlyList<RendimientoProducto> RendimientoPorProducto(PeriodoReporte periodo);

    /// <summary>La venta con sus renglones, o null si no existe.</summary>
    DetalleVenta? ObtenerDetalle(long ventaId);

    /// <summary>
    /// Marca la venta como anulada y devuelve el stock de sus productos, en una sola operación atómica.
    /// </summary>
    void Anular(long ventaId, string motivo, DateTime fecha);
}
