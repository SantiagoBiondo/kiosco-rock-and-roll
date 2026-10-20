using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KioscoRockAndRoll.Reportes;

/// <summary>Piezas comunes a todos los PDF, para que se vean iguales.</summary>
internal static class EstiloPdf
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    public static readonly string ColorTitulos = Colors.Grey.Darken3;
    public static readonly string ColorSuave = Colors.Grey.Darken1;
    public static readonly string ColorLinea = Colors.Grey.Lighten2;
    public static readonly string ColorPerdida = Colors.Red.Darken2;

    /// <summary>Una página A4 con encabezado (negocio + subtítulo), el contenido y el pie con la fecha y el número de página.</summary>
    public static void Pagina(IDocumentContainer documento, string nombreNegocio, string subtitulo, DateTime generadoEn,
        Action<ColumnDescriptor> contenido)
    {
        documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.5f, Unit.Centimetre);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

            pagina.Header().BorderBottom(1).BorderColor(ColorLinea).PaddingBottom(6).Column(columna =>
            {
                columna.Item().Text(nombreNegocio).FontSize(18).Bold().FontColor(ColorTitulos);
                columna.Item().Text(subtitulo).FontSize(12);
            });
            pagina.Content().PaddingVertical(12).Column(columna =>
            {
                columna.Spacing(18);
                contenido(columna);
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

    public static string Plata(decimal monto) => monto.ToString("C2", Cultura);

    public static string Porcentaje(decimal? valor) => valor?.ToString("P0", Cultura) ?? "-";

    public static Action<IContainer> Titulo(string texto) => contenedor =>
        contenedor.PaddingBottom(6).Text(texto).FontSize(13).Bold().FontColor(ColorTitulos);

    /// <summary>Un número grande con su etiqueta arriba (para los resúmenes).</summary>
    public static Action<IContainer> Dato(string etiqueta, string valor, bool destacado = false, string? color = null) => contenedor =>
        contenedor.Column(columna =>
        {
            columna.Item().Text(etiqueta).FontSize(9).FontColor(ColorSuave);
            var texto = columna.Item().Text(valor).FontSize(destacado ? 16 : 13);
            if (destacado)
                texto.Bold();
            if (color != null)
                texto.FontColor(color);
        });

    /// <summary>Una fila "concepto ...... valor" sin bordes.</summary>
    public static void Fila(TableDescriptor tabla, string concepto, string valor, bool negrita = false)
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
    public static void Encabezado(IContainer celda, string texto, bool derecha = false) =>
        Alinear(celda.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(3).PaddingHorizontal(3), derecha)
            .Text(texto).Bold();

    public static TextBlockDescriptor Celda(IContainer celda, string texto, bool derecha = false) =>
        Alinear(celda.BorderBottom(0.5f).BorderColor(ColorLinea).PaddingVertical(3).PaddingHorizontal(3), derecha)
            .Text(texto);

    private static IContainer Alinear(IContainer contenedor, bool derecha) =>
        derecha ? contenedor.AlignRight() : contenedor.AlignLeft();
}
