using KioscoRockAndRoll.Dominio.Entidades;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static KioscoRockAndRoll.Reportes.EstiloPdf;

namespace KioscoRockAndRoll.Reportes;

/// <summary>PDF del reporte de ventas y ganancia de un período, con el ranking de productos.</summary>
public sealed class ReporteDeVentasPdf(ReporteDeVentas reporte, OrdenProductos orden, string nombreNegocio, DateTime generadoEn)
    : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Reporte de ventas {reporte.Periodo.Descripcion}",
        Author = nombreNegocio,
        CreationDate = generadoEn
    };

    public void Compose(IDocumentContainer container) =>
        Pagina(container, nombreNegocio, $"Reporte de ventas y ganancia: {reporte.Periodo.Descripcion}", generadoEn,
            columna =>
            {
                columna.Item().Element(Resumen);
                columna.Item().Element(PorPeriodo);
                columna.Item().Element(Productos);
                columna.Item().Text(
                        "La ganancia es lo vendido menos el costo de cada producto al momento de la venta. " +
                        "El porcentaje es sobre el costo. Las ventas anuladas no se cuentan.")
                    .FontSize(8).FontColor(ColorSuave);
            });

    private void Resumen(IContainer contenedor) => contenedor.Column(columna =>
    {
        columna.Item().Element(Titulo("Resumen"));
        columna.Item().Row(fila =>
        {
            fila.RelativeItem().Element(Dato("Vendido", Plata(reporte.Vendido), destacado: true));
            fila.RelativeItem().Element(Dato("Costo", Plata(reporte.Costo)));
            fila.RelativeItem().Element(Dato("Ganancia", Plata(reporte.Ganancia), destacado: true,
                color: reporte.Ganancia < 0 ? ColorPerdida : null));
            fila.RelativeItem().Element(Dato("% sobre costo", Porcentaje(reporte.PorcentajeSobreCosto)));
            fila.RelativeItem().Element(Dato("Ventas", reporte.CantidadVentas.ToString("N0", Cultura)));
        });
    });

    private void PorPeriodo(IContainer contenedor) => contenedor.Column(columna =>
    {
        columna.Item().Element(Titulo(reporte.Agrupacion == Agrupacion.PorMes ? "Por mes" : "Por día"));
        columna.Item().Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(55);
                c.ConstantColumn(90);
                c.ConstantColumn(90);
                c.ConstantColumn(90);
                c.ConstantColumn(55);
            });
            tabla.Header(encabezado =>
            {
                Encabezado(encabezado.Cell(), reporte.Agrupacion == Agrupacion.PorMes ? "Mes" : "Día");
                Encabezado(encabezado.Cell(), "Ventas", derecha: true);
                Encabezado(encabezado.Cell(), "Vendido", derecha: true);
                Encabezado(encabezado.Cell(), "Costo", derecha: true);
                Encabezado(encabezado.Cell(), "Ganancia", derecha: true);
                Encabezado(encabezado.Cell(), "%", derecha: true);
            });
            foreach (var fila in reporte.Filas)
            {
                var sinVentas = fila.CantidadVentas == 0;
                Gris(Celda(tabla.Cell(), fila.Etiqueta), sinVentas);
                Gris(Celda(tabla.Cell(), fila.CantidadVentas.ToString("N0", Cultura), derecha: true), sinVentas);
                Gris(Celda(tabla.Cell(), Plata(fila.Vendido), derecha: true), sinVentas);
                Gris(Celda(tabla.Cell(), Plata(fila.Costo), derecha: true), sinVentas);
                ColorGanancia(Celda(tabla.Cell(), Plata(fila.Ganancia), derecha: true), fila.Ganancia, sinVentas);
                Gris(Celda(tabla.Cell(), Porcentaje(fila.PorcentajeSobreCosto), derecha: true), sinVentas);
            }
        });
    });

    private void Productos(IContainer contenedor) => contenedor.Column(columna =>
    {
        var titulo = orden switch
        {
            OrdenProductos.PorUnidades => "Productos más vendidos (por unidades)",
            OrdenProductos.PorGanancia => "Productos que más ganancia dejaron",
            _ => "Productos que más recaudaron"
        };
        columna.Item().Element(Titulo(titulo));
        var productos = reporte.ProductosOrdenados(orden);
        if (productos.Count == 0)
        {
            columna.Item().Text("No hubo ventas en el período.").FontColor(ColorSuave);
            return;
        }

        columna.Item().Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30);
                c.RelativeColumn();
                c.ConstantColumn(60);
                c.ConstantColumn(90);
                c.ConstantColumn(90);
                c.ConstantColumn(55);
            });
            tabla.Header(encabezado =>
            {
                Encabezado(encabezado.Cell(), "#");
                Encabezado(encabezado.Cell(), "Producto");
                Encabezado(encabezado.Cell(), "Unidades", derecha: true);
                Encabezado(encabezado.Cell(), "Vendido", derecha: true);
                Encabezado(encabezado.Cell(), "Ganancia", derecha: true);
                Encabezado(encabezado.Cell(), "%", derecha: true);
            });
            var puesto = 1;
            foreach (var producto in productos)
            {
                Celda(tabla.Cell(), (puesto++).ToString(Cultura));
                Celda(tabla.Cell(), producto.Nombre);
                Celda(tabla.Cell(), producto.Unidades.ToString("N0", Cultura), derecha: true);
                Celda(tabla.Cell(), Plata(producto.Vendido), derecha: true);
                ColorGanancia(Celda(tabla.Cell(), Plata(producto.Ganancia), derecha: true), producto.Ganancia, apagado: false);
                Celda(tabla.Cell(), Porcentaje(producto.PorcentajeSobreCosto), derecha: true);
            }
        });
    });

    private static void Gris(TextBlockDescriptor texto, bool apagado)
    {
        if (apagado)
            texto.FontColor(ColorSuave);
    }

    private static void ColorGanancia(TextBlockDescriptor texto, decimal ganancia, bool apagado)
    {
        if (ganancia < 0)
            texto.FontColor(ColorPerdida);
        else
            Gris(texto, apagado);
    }
}
