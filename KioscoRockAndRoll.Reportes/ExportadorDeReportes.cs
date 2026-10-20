using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace KioscoRockAndRoll.Reportes;

/// <summary>Genera los PDF de ventas y los guarda en disco.</summary>
public sealed class ExportadorDeReportes(ReporteDelDiaServicio reportes, TimeProvider reloj)
{
    public const string NombreNegocio = "Kiosco Rock & Roll";

    static ExportadorDeReportes()
    {
        // Licencia gratuita de QuestPDF para negocios que facturan menos de 1 millón de dólares al año.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Documentos\Kiosco Rock &amp; Roll\Ventas</summary>
    public static string CarpetaPorDefecto { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), NombreNegocio, "Ventas");

    public static string NombreDeArchivo(DateOnly dia) => $"Ventas {dia:yyyy-MM-dd}.pdf";

    /// <summary>El PDF de las ventas del día, en memoria.</summary>
    public byte[] VentasDelDia(DateOnly dia) =>
        new ReporteVentasDelDia(reportes.Datos(dia), NombreNegocio, reloj.GetLocalNow().DateTime).GeneratePdf();

    public static string NombreDeArchivo(PeriodoReporte periodo) => periodo.Desde == periodo.Hasta
        ? $"Reporte {periodo.Desde:yyyy-MM-dd}.pdf"
        : $"Reporte {periodo.Desde:yyyy-MM-dd} a {periodo.Hasta:yyyy-MM-dd}.pdf";

    /// <summary>Guarda el PDF de un reporte de ventas y ganancia.</summary>
    /// <returns>La ruta del archivo guardado.</returns>
    public string GuardarReporte(ReporteDeVentas reporte, OrdenProductos orden, string ruta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
        var documento = new ReporteDeVentasPdf(reporte, orden, NombreNegocio, reloj.GetLocalNow().DateTime);
        File.WriteAllBytes(ruta, documento.GeneratePdf());
        return ruta;
    }

    /// <summary>
    /// Guarda el PDF de las ventas del día. Si no se indica ruta, va a la carpeta por defecto
    /// (si ya existe el archivo de ese día, se reemplaza con los datos actualizados).
    /// </summary>
    /// <returns>La ruta del archivo guardado.</returns>
    public string GuardarVentasDelDia(DateOnly dia, string? ruta = null)
    {
        ruta ??= Path.Combine(CarpetaPorDefecto, NombreDeArchivo(dia));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
        File.WriteAllBytes(ruta, VentasDelDia(dia));
        return ruta;
    }
}
