namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    // Ayudas para que todas las pantallas se vean parecidas.
    public static class Estilos
    {
        public static readonly Font Normal = new("Segoe UI", 11F);
        public static readonly Font Grande = new("Segoe UI", 16F);
        public static readonly Font Total = new("Segoe UI", 28F, FontStyle.Bold);

        public static readonly Color FondoStockBajo = Color.FromArgb(255, 220, 220);
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
    }
}
