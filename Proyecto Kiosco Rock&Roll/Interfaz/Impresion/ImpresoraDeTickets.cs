using System.ComponentModel;
using System.Drawing.Printing;
using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Reportes;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Impresion
{
    /// <summary>No se pudo imprimir (impresora apagada, desconectada, sin elegir...). El mensaje es para el usuario.</summary>
    public sealed class NoSePudoImprimirException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

    /// <summary>
    /// Imprime tickets por la impresora elegida en la configuración, usando el driver de Windows:
    /// sirve para impresoras térmicas de 58 u 80 mm y también para impresoras comunes.
    /// </summary>
    public sealed class ImpresoraDeTickets(AjustesDeTicket ajustes)
    {
        private const string Letra = "Consolas";       // de ancho fijo: las columnas quedan alineadas
        private const float MargenCentesimas = 8;      // 2 mm arriba y abajo
        private const float AvanceFinalCentesimas = 60; // papel extra al final para poder cortarlo

        public ConfiguracionDeTicket Configuracion => ajustes.Leer();

        /// <summary>Las impresoras instaladas en Windows.</summary>
        public static IReadOnlyList<string> ImpresorasInstaladas() =>
            PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

        /// <summary>Imprime el ticket por la impresora configurada.</summary>
        /// <exception cref="NoSePudoImprimirException">Si no hay impresora elegida o falló la impresión.</exception>
        public void Imprimir(Ticket ticket) => Imprimir(ticket, ajustes.Leer());

        /// <param name="aArchivo">
        /// Si se indica, la impresión va a ese archivo (sirve con impresoras como "Microsoft Print to PDF").
        /// </param>
        public static void Imprimir(Ticket ticket, ConfiguracionDeTicket configuracion, string? aArchivo = null)
        {
            if (!configuracion.HayImpresora)
                throw new NoSePudoImprimirException("No hay ninguna impresora elegida. Elegila en la sección Configuración.");

            var renglones = new FormatoDeTicket(configuracion.Columnas).Renglones(ticket);
            using var fuente = FuenteQueEntra(configuracion);
            var altoRenglon = fuente.GetHeight(100); // en centésimas de pulgada, la unidad de la impresión

            using var documento = new PrintDocument { DocumentName = $"Ticket venta {ticket.NumeroVenta}" };
            documento.PrinterSettings.PrinterName = configuracion.Impresora;
            if (!documento.PrinterSettings.IsValid)
                throw new NoSePudoImprimirException($"No se encontró la impresora \"{configuracion.Impresora}\". ¿Está conectada?");
            if (aArchivo != null)
            {
                documento.PrinterSettings.PrintToFile = true;
                documento.PrinterSettings.PrintFileName = aArchivo;
            }

            // Un papel tan largo como el ticket (las impresoras de rollo lo cortan donde termina).
            var anchoPapel = (int)Math.Round((double)configuracion.Ancho / 25.4 * 100);
            var altoPapel = (int)Math.Ceiling(MargenCentesimas * 2 + renglones.Count * altoRenglon + AvanceFinalCentesimas);
            documento.DefaultPageSettings.PaperSize = new PaperSize("Ticket", anchoPapel, altoPapel);
            documento.DefaultPageSettings.Margins = new Margins(0, 0, (int)MargenCentesimas, (int)MargenCentesimas);
            documento.PrintController = new StandardPrintController(); // sin el cartelito de "Imprimiendo..."

            documento.PrintPage += (_, e) =>
            {
                var y = MargenCentesimas;
                foreach (var renglon in renglones)
                {
                    e.Graphics!.DrawString(renglon, fuente, Brushes.Black, 0, y, StringFormat.GenericTypographic);
                    y += altoRenglon;
                }
                e.HasMorePages = false;
            };

            try
            {
                documento.Print();
            }
            catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception or InvalidOperationException)
            {
                throw new NoSePudoImprimirException($"No se pudo imprimir: {ex.Message}", ex);
            }
        }

        /// <summary>Los renglones del ticket como texto, para mostrar una vista previa en pantalla.</summary>
        public static string VistaPrevia(Ticket ticket, ConfiguracionDeTicket configuracion) =>
            string.Join(Environment.NewLine, new FormatoDeTicket(configuracion.Columnas).Renglones(ticket));

        /// <summary>
        /// La letra del tamaño justo para que entren las columnas del ticket en el ancho imprimible del papel.
        /// </summary>
        private static Font FuenteQueEntra(ConfiguracionDeTicket configuracion)
        {
            const float tamanioDePrueba = 10f;
            using var prueba = new Font(Letra, tamanioDePrueba);
            using var pantalla = Graphics.FromHwnd(IntPtr.Zero);
            var anchoPulgadas = pantalla.MeasureString(new string('M', configuracion.Columnas), prueba,
                int.MaxValue, StringFormat.GenericTypographic).Width / pantalla.DpiX;
            var anchoDisponible = (float)configuracion.AnchoImprimibleMm / 25.4f;
            return new Font(Letra, tamanioDePrueba * anchoDisponible / anchoPulgadas);
        }

        /// <summary>Un ticket inventado para probar la impresora.</summary>
        public static Ticket TicketDePrueba() => new(
            ExportadorDeReportes.NombreNegocio, 0, DateTime.Now,
            [
                new LineaTicket(2, "Coca-Cola 500 ml", 1650),
                new LineaTicket(1, "Alfajor triple de chocolate con dulce de leche", 880)
            ],
            [new Pago(MedioDePago.Efectivo, 4180)],
            EfectivoRecibido: 5000,
            EsCopia: false);
    }
}
