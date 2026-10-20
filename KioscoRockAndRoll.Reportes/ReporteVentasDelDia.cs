using System.Globalization;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KioscoRockAndRoll.Reportes;

/// <summary>
/// PDF con las ventas de un día: resumen, turnos de caja, productos vendidos y detalle de cada venta.
/// </summary>
public sealed class ReporteVentasDelDia(DatosDelDia datos, string nombreNegocio, DateTime generadoEn) : IDocument
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private static readonly string ColorTitulos = Colors.Grey.Darken3;
    private static readonly string ColorSuave = Colors.Grey.Darken1;
    private static readonly string ColorLinea = Colors.Grey.Lighten2;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Ventas del {datos.Dia:dd/MM/yyyy}",
        Author = nombreNegocio,
        CreationDate = generadoEn
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.5f, Unit.Centimetre);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

            pagina.Header().Element(Encabezado);
            pagina.Content().PaddingVertical(12).Column(columna =>
            {
                columna.Spacing(18);
                columna.Item().Element(Resumen);
                columna.Item().Element(Turnos);
                columna.Item().Element(Productos);
                columna.Item().Element(DetalleDeVentas);
            });
            pagina.Footer().AlignCenter().Text(texto =>
            {
                texto.DefaultTextStyle(estilo => estilo.FontSize(8).FontColor(ColorSuave));
                texto.Span($"Generado el {generadoEn.ToString("dd/MM/yyyy HH:mm", Cultura)}  ·  Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });
    }

    private void Encabezado(IContainer contenedor)
    {
        var fecha = datos.Dia.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);
        contenedor.BorderBottom(1).BorderColor(ColorLinea).PaddingBottom(6).Column(columna =>
        {
            columna.Item().Text(nombreNegocio).FontSize(18).Bold().FontColor(ColorTitulos);
            columna.Item().Text($"Ventas del {fecha}").FontSize(12);
        });
    }

    private void Resumen(IContainer contenedor)
    {
        var ventas = datos.Ventas;
        contenedor.Column(columna =>
        {
            columna.Item().Element(Titulo("Resumen"));
            columna.Item().Row(fila =>
            {
                fila.RelativeItem().Element(Dato("Total vendido", Plata(ventas.Total), destacado: true));
                fila.RelativeItem().Element(Dato("Ventas", ventas.CantidadVigentes.ToString(Cultura)));
                fila.RelativeItem().Element(Dato("Anuladas", ventas.CantidadAnuladas.ToString(Cultura)));
            });
            if (ventas.TotalPorMedio.Count > 0)
            {
                columna.Item().PaddingTop(6).Text(texto =>
                {
                    texto.Span("Por medio de pago: ").FontColor(ColorSuave);
                    texto.Span(string.Join("   ·   ",
                        ventas.TotalPorMedio.OrderBy(p => p.Key).Select(p => $"{p.Key.Descripcion()} {Plata(p.Value)}")));
                });
            }
        });
    }

    private void Turnos(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Element(Titulo("Caja"));
            if (datos.Turnos.Count == 0)
            {
                columna.Item().Text("No se abrió la caja este día.").FontColor(ColorSuave);
                return;
            }

            foreach (var turno in datos.Turnos)
            {
                columna.Item().PaddingBottom(8).Column(t =>
                {
                    var apertura = turno.Apertura.ToString("HH:mm", Cultura);
                    t.Item().Text(turno.Cierre is { } cierre
                        ? $"Turno de {apertura} a {cierre.ToString("HH:mm", Cultura)}"
                        : $"Turno desde las {apertura} (la caja sigue abierta)").Bold();
                    t.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(110); });
                        Fila(tabla, "Efectivo inicial", Plata(turno.EfectivoInicial));
                        Fila(tabla, "+ Ventas en efectivo", Plata(turno.VentasEnEfectivo));
                        Fila(tabla, "+ Ingresos de efectivo", Plata(turno.Ingresos));
                        Fila(tabla, "− Retiros de efectivo", Plata(turno.Retiros));
                        Fila(tabla, "= Efectivo esperado", Plata(turno.EfectivoEsperado), negrita: true);
                        if (turno.EfectivoContado is { } contado)
                        {
                            Fila(tabla, "Efectivo contado", Plata(contado), negrita: true);
                            Fila(tabla, "Diferencia", ResumenTurno.DescribirDiferencia(turno.Diferencia ?? 0), negrita: true);
                        }
                    });
                    foreach (var movimiento in turno.Movimientos)
                    {
                        t.Item().PaddingLeft(10).Text(
                                $"{movimiento.Fecha.ToString("HH:mm", Cultura)}  {movimiento.TipoDescripcion} {Plata(movimiento.Monto)}: {movimiento.Motivo}")
                            .FontSize(9).FontColor(ColorSuave);
                    }
                });
            }
        });
    }

    private void Productos(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Element(Titulo("Productos vendidos"));
            if (datos.Productos.Count == 0)
            {
                columna.Item().Text("No hubo ventas.").FontColor(ColorSuave);
                return;
            }

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(70); c.ConstantColumn(100); });
                tabla.Header(encabezado =>
                {
                    Encabezado(encabezado.Cell(), "Producto");
                    Encabezado(encabezado.Cell(), "Unidades", derecha: true);
                    Encabezado(encabezado.Cell(), "Total", derecha: true);
                });
                foreach (var producto in datos.Productos)
                {
                    Celda(tabla.Cell(), producto.Nombre);
                    Celda(tabla.Cell(), producto.Cantidad.ToString("N0", Cultura), derecha: true);
                    Celda(tabla.Cell(), Plata(producto.Total), derecha: true);
                }
            });
        });
    }

    private void DetalleDeVentas(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Element(Titulo("Detalle de ventas"));
            if (datos.Ventas.Ventas.Count == 0)
            {
                columna.Item().Text("No hubo ventas.").FontColor(ColorSuave);
                return;
            }

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(40);   // Nº
                    c.ConstantColumn(45);   // hora
                    c.RelativeColumn();     // medio de pago / anulación
                    c.ConstantColumn(60);   // unidades
                    c.ConstantColumn(90);   // total
                });
                tabla.Header(encabezado =>
                {
                    Encabezado(encabezado.Cell(), "Nº");
                    Encabezado(encabezado.Cell(), "Hora");
                    Encabezado(encabezado.Cell(), "Medio de pago");
                    Encabezado(encabezado.Cell(), "Unidades", derecha: true);
                    Encabezado(encabezado.Cell(), "Total", derecha: true);
                });
                foreach (var venta in datos.Ventas.Ventas)
                {
                    var medio = venta.Anulada
                        ? $"{venta.MedioDePagoDescripcion} — ANULADA: {venta.MotivoAnulacion}"
                        : venta.MedioDePagoDescripcion;
                    Celda(tabla.Cell(), venta.Id.ToString(Cultura), venta.Anulada);
                    Celda(tabla.Cell(), venta.Fecha.ToString("HH:mm", Cultura), venta.Anulada);
                    Celda(tabla.Cell(), medio, venta.Anulada);
                    Celda(tabla.Cell(), venta.Unidades.ToString("N0", Cultura), venta.Anulada, derecha: true);
                    Celda(tabla.Cell(), Plata(venta.Total), venta.Anulada, derecha: true);
                }
            });
            if (datos.Ventas.CantidadAnuladas > 0)
                columna.Item().PaddingTop(4).Text("Las ventas anuladas no suman en los totales.").FontSize(8).FontColor(ColorSuave);
        });
    }

    // ---------- Piezas reutilizables ----------

    private static string Plata(decimal monto) => monto.ToString("C2", Cultura);

    private static Action<IContainer> Titulo(string texto) => contenedor =>
        contenedor.PaddingBottom(6).Text(texto).FontSize(13).Bold().FontColor(ColorTitulos);

    private static Action<IContainer> Dato(string etiqueta, string valor, bool destacado = false) => contenedor =>
        contenedor.Column(columna =>
        {
            columna.Item().Text(etiqueta).FontSize(9).FontColor(ColorSuave);
            var texto = columna.Item().Text(valor).FontSize(destacado ? 16 : 13);
            if (destacado)
                texto.Bold();
        });

    private static void Fila(TableDescriptor tabla, string concepto, string valor, bool negrita = false)
    {
        var izquierda = tabla.Cell().PaddingVertical(1).Text(concepto);
        var derecha = tabla.Cell().PaddingVertical(1).AlignRight().Text(valor);
        if (negrita)
        {
            izquierda.Bold();
            derecha.Bold();
        }
    }

    // La alineación va después del borde: si se aplicara antes, la celda se achicaría
    // al ancho del texto y la línea de abajo quedaría cortada.
    private static void Encabezado(IContainer celda, string texto, bool derecha = false) =>
        Alinear(celda.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(3).PaddingHorizontal(3), derecha)
            .Text(texto).Bold();

    private static void Celda(IContainer celda, string texto, bool anulada = false, bool derecha = false)
    {
        var contenido = Alinear(celda.BorderBottom(0.5f).BorderColor(ColorLinea).PaddingVertical(3).PaddingHorizontal(3), derecha)
            .Text(texto);
        if (anulada)
            contenido.Strikethrough().FontColor(ColorSuave);
    }

    private static IContainer Alinear(IContainer contenedor, bool derecha) =>
        derecha ? contenedor.AlignRight() : contenedor.AlignLeft();
}
