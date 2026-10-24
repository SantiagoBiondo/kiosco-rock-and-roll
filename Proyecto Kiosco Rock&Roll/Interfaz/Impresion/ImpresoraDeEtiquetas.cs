using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using KioscoRockAndRoll.Dominio.Entidades;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Impresion
{
    /// <summary>
    /// Imprime etiquetas de precio en hojas A4: una grilla de 3 columnas por 8 filas, con líneas
    /// punteadas para recortarlas. Cada etiqueta lleva el nombre del producto y el precio bien grande.
    /// </summary>
    public static class ImpresoraDeEtiquetas
    {
        public const int Columnas = 3;
        public const int Filas = 8;
        public const int PorHoja = Columnas * Filas;

        private const float MargenHojaMm = 10;
        private const float MargenEtiquetaMm = 2;
        private const string Letra = "Segoe UI";
        private const float TamanioNombre = 11;
        private const float TamanioPrecioMaximo = 30;

        /// <summary>
        /// Arma el documento con todas las hojas. Sirve tanto para imprimir como para la vista previa.
        /// </summary>
        /// <param name="impresora">La impresora (null = la predeterminada de Windows).</param>
        public static PrintDocument Documento(IReadOnlyList<Etiqueta> etiquetas, string? impresora)
        {
            var documento = new PrintDocument { DocumentName = "Etiquetas de precios" };
            if (impresora != null)
                documento.PrinterSettings.PrinterName = impresora;
            documento.DefaultPageSettings.Landscape = false;
            documento.DefaultPageSettings.PaperSize = A4De(documento.PrinterSettings);

            var siguiente = 0;
            documento.BeginPrint += (_, _) => siguiente = 0;
            documento.PrintPage += (_, e) =>
            {
                // En papel, el origen está donde empieza la zona imprimible; en la vista previa, en la esquina.
                var corrimiento = documento.PrintController?.IsPreview == true
                    ? PointF.Empty
                    : new PointF(e.PageSettings.HardMarginX, e.PageSettings.HardMarginY);
                var hoja = etiquetas.Skip(siguiente).Take(PorHoja).ToList();
                DibujarHoja(e.Graphics!, e.PageBounds, corrimiento, hoja);
                siguiente += PorHoja;
                e.HasMorePages = siguiente < etiquetas.Count;
            };
            return documento;
        }

        /// <summary>Imprime las etiquetas por la impresora indicada.</summary>
        /// <exception cref="NoSePudoImprimirException">Si no hay etiquetas, no está la impresora o falló la impresión.</exception>
        public static void Imprimir(IReadOnlyList<Etiqueta> etiquetas, string? impresora)
        {
            if (etiquetas.Count == 0)
                throw new NoSePudoImprimirException("No hay etiquetas para imprimir: escaneá algún producto.");

            using var documento = Documento(etiquetas, impresora);
            if (!documento.PrinterSettings.IsValid)
                throw new NoSePudoImprimirException($"No se encontró la impresora \"{impresora}\". ¿Está conectada?");
            documento.PrintController = new StandardPrintController(); // sin el cartelito de "Imprimiendo..."

            try
            {
                documento.Print();
            }
            catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception or InvalidOperationException)
            {
                throw new NoSePudoImprimirException($"No se pudo imprimir: {ex.Message}", ex);
            }
        }

        /// <summary>El precio como se lee en la góndola: sin centavos si es redondo.</summary>
        public static string TextoPrecio(decimal precio) =>
            precio == decimal.Truncate(precio) ? precio.ToString("C0") : precio.ToString("C2");

        private static PaperSize A4De(PrinterSettings impresora)
        {
            try
            {
                if (impresora.IsValid &&
                    impresora.PaperSizes.Cast<PaperSize>().FirstOrDefault(p => p.Kind == PaperKind.A4) is { } a4)
                    return a4;
            }
            catch (Win32Exception)
            {
                // Sin impresoras instaladas no se pueden consultar los papeles: se usa la medida de A4.
            }
            return new PaperSize("A4", 827, 1169); // en centésimas de pulgada
        }

        /// <summary>Milímetros a centésimas de pulgada, la unidad en la que se dibuja la hoja.</summary>
        private static float Cent(float mm) => mm / 25.4f * 100;

        private static void DibujarHoja(Graphics g, Rectangle pagina, PointF corrimiento, IReadOnlyList<Etiqueta> etiquetas)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(-corrimiento.X, -corrimiento.Y);

            var margen = Cent(MargenHojaMm);
            var ancho = (pagina.Width - 2 * margen) / Columnas;
            var alto = (pagina.Height - 2 * margen) / Filas;

            using var corte = new Pen(Color.DarkGray, 1) { DashStyle = DashStyle.Dash };
            using var fuenteNombre = new Font(Letra, TamanioNombre, FontStyle.Bold);
            using var centrado = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisWord
            };

            for (var i = 0; i < etiquetas.Count; i++)
            {
                var celda = new RectangleF(margen + i % Columnas * ancho, margen + i / Columnas * alto, ancho, alto);
                g.DrawRectangle(corte, celda.X, celda.Y, celda.Width, celda.Height);

                var interior = RectangleF.Inflate(celda, -Cent(MargenEtiquetaMm), -Cent(MargenEtiquetaMm));
                var zonaNombre = new RectangleF(interior.X, interior.Y, interior.Width, interior.Height * 0.45f);
                var zonaPrecio = new RectangleF(interior.X, zonaNombre.Bottom, interior.Width, interior.Height - zonaNombre.Height);

                g.DrawString(etiquetas[i].Nombre, fuenteNombre, Brushes.Black, zonaNombre, centrado);

                var precio = TextoPrecio(etiquetas[i].Precio);
                using var fuentePrecio = FuenteQueEntra(g, precio, zonaPrecio.Width);
                g.DrawString(precio, fuentePrecio, Brushes.Black, zonaPrecio, centrado);
            }
        }

        /// <summary>La letra del precio lo más grande posible sin pasarse del ancho de la etiqueta.</summary>
        private static Font FuenteQueEntra(Graphics g, string texto, float anchoDisponible)
        {
            var tamanio = TamanioPrecioMaximo;
            while (true)
            {
                var fuente = new Font(Letra, tamanio, FontStyle.Bold);
                if (tamanio <= 12 || g.MeasureString(texto, fuente).Width <= anchoDisponible)
                    return fuente;
                fuente.Dispose();
                tamanio -= 2;
            }
        }
    }
}
