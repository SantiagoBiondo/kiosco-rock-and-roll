using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static KioscoRockAndRoll.Reportes.EstiloPdf;

namespace KioscoRockAndRoll.Reportes;

/// <summary>
/// PDF con las ventas de un día: resumen, turnos de caja, productos vendidos y detalle de cada venta.
/// </summary>
public sealed class ReporteVentasDelDia(DatosDelDia datos, string nombreNegocio, DateTime generadoEn) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Ventas del {datos.Dia:dd/MM/yyyy}",
        Author = nombreNegocio,
        CreationDate = generadoEn
    };

    public void Compose(IDocumentContainer container) =>
        Pagina(container, nombreNegocio, $"Ventas del {datos.Dia.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura)}", generadoEn,
            columna =>
            {
                columna.Item().Element(Resumen);
                columna.Item().Element(Turnos);
                columna.Item().Element(Productos);
                columna.Item().Element(DetalleDeVentas);
            });

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
                    CeldaDeVenta(tabla.Cell(), venta.Id.ToString(Cultura), venta.Anulada);
                    CeldaDeVenta(tabla.Cell(), venta.Fecha.ToString("HH:mm", Cultura), venta.Anulada);
                    CeldaDeVenta(tabla.Cell(), medio, venta.Anulada);
                    CeldaDeVenta(tabla.Cell(), venta.Unidades.ToString("N0", Cultura), venta.Anulada, derecha: true);
                    CeldaDeVenta(tabla.Cell(), Plata(venta.Total), venta.Anulada, derecha: true);
                }
            });
            if (datos.Ventas.CantidadAnuladas > 0)
                columna.Item().PaddingTop(4).Text("Las ventas anuladas no suman en los totales.").FontSize(8).FontColor(ColorSuave);
        });
    }

    private static void CeldaDeVenta(IContainer celda, string texto, bool anulada, bool derecha = false)
    {
        var contenido = Celda(celda, texto, derecha);
        if (anulada)
            contenido.Strikethrough().FontColor(ColorSuave);
    }
}
