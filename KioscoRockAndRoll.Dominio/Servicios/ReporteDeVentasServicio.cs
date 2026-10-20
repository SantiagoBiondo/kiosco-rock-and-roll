using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Reportes de ventas y ganancia por período.</summary>
public class ReporteDeVentasServicio(IVentaRepositorio ventas)
{
    public ReporteDeVentas Generar(PeriodoReporte periodo, Agrupacion agrupacion) =>
        ReporteDeVentas.Armar(periodo, agrupacion, ventas.TotalesPorDia(periodo), ventas.RendimientoPorProducto(periodo));
}
