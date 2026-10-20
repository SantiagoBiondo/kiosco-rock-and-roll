using System.Globalization;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz
{
    /// <summary>Ayudas para que todas las pantallas se vean parecidas.</summary>
    public static class Estilos
    {
        public static readonly Font Normal = new("Segoe UI", 11F);
        public static readonly Font Grande = new("Segoe UI", 16F);
        public static readonly Font Titulo = new("Segoe UI", 15F, FontStyle.Bold);
        public static readonly Font Total = new("Segoe UI", 28F, FontStyle.Bold);

        public static readonly Color FondoStockBajo = Color.FromArgb(255, 220, 220);
        public static readonly Color FondoPanelLateral = Color.FromArgb(243, 246, 250);
        public static readonly Color FondoBarra = Color.FromArgb(32, 36, 44);
        public static readonly Color SeccionActiva = Color.FromArgb(220, 53, 69);
        public static readonly Color Advertencia = Color.DarkOrange;
        public static readonly Color Exito = Color.ForestGreen;

        public static void ConfigurarGrilla(DataGridView grilla)
        {
            grilla.Dock = DockStyle.Fill;
            grilla.AutoGenerateColumns = false;
            grilla.AllowUserToAddRows = false;
            grilla.AllowUserToDeleteRows = false;
            grilla.AllowUserToResizeRows = false;
            grilla.MultiSelect = false;
            grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grilla.RowHeadersVisible = false;
            grilla.BackgroundColor = SystemColors.Window;
            grilla.Font = Normal;
            grilla.RowTemplate.Height = 32;
            grilla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        }

        public static DataGridViewTextBoxColumn Columna(
            DataGridView grilla, string propiedad, string titulo,
            string? formato = null, int ancho = 120, bool llenar = false, bool soloLectura = true)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                DataPropertyName = propiedad,
                Name = propiedad,
                HeaderText = titulo,
                ReadOnly = soloLectura,
                Width = ancho,
                AutoSizeMode = llenar ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            if (formato != null)
            {
                columna.DefaultCellStyle.Format = formato;
                columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            grilla.Columns.Add(columna);
            return columna;
        }

        public static Button Boton(string texto, EventHandler alHacerClic, int ancho = 170)
        {
            var boton = new Button { Text = texto, Width = ancho, Height = 42, Font = Normal, Margin = new Padding(4) };
            boton.Click += alHacerClic;
            return boton;
        }

        public static NumericUpDown CampoNumero(int decimales, decimal maximo = 100_000_000)
        {
            return new NumericUpDown
            {
                DecimalPlaces = decimales,
                Maximum = maximo,
                Minimum = 0,
                ThousandsSeparator = true,
                Width = 200,
                Font = Normal,
                TextAlign = HorizontalAlignment.Right
            };
        }

        /// <summary>
        /// Lee lo que está escrito en el campo aunque todavía no se haya confirmado
        /// (Value recién se actualiza al salir del campo).
        /// </summary>
        public static decimal ValorTipeado(this NumericUpDown campo) =>
            decimal.TryParse(campo.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var valor) ? valor : 0;

        public static Label Etiqueta(string texto, Font? fuente = null) =>
            new() { Text = texto, AutoSize = true, Font = fuente ?? Normal, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) };
    }
}
