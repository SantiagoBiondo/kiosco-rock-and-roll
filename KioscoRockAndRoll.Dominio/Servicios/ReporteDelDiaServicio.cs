using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Todo lo que hace falta para el reporte de un día.</summary>
public sealed record DatosDelDia(
    ResumenDelDia Ventas,
    IReadOnlyList<ResumenTurno> Turnos,
    IReadOnlyList<ProductoVendido> Productos)
{
    public DateOnly Dia => Ventas.Dia;
}

/// <summary>Junta la información de ventas y caja de un día para armar el reporte.</summary>
public class ReporteDelDiaServicio(VentaServicio ventas, CajaServicio caja)
{
    public DatosDelDia Datos(DateOnly dia) =>
        new(ventas.ResumenDelDia(dia), caja.TurnosDelDia(dia), ventas.ProductosVendidos(dia));
}
