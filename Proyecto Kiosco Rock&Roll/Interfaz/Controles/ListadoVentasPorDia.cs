using System.Diagnostics;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using KioscoRockAndRoll.Reportes;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Ventas de un día con totales por medio de pago. Las anuladas se ven en gris y no suman.
    /// Enter o doble clic sobre una venta pide ver su detalle.
    /// </summary>
    public class ListadoVentasPorDia : UserControl
    {
        private readonly VentaServicio _ventas;
        private readonly ExportadorDeReportes _exportador;

        private readonly DateTimePicker dtpDia = new() { Format = DateTimePickerFormat.Long, Width = 300 };
        private readonly DataGridView grilla = new();
        private readonly Label lblResumen = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), Font = Estilos.Grande };
        private readonly Label lblEstado = new() { AutoSize = true, Margin = new Padding(12, 14, 3, 3), MaximumSize = new Size(700, 0) };

        /// <summary>Se eligió una venta para ver su detalle (o anularla).</summary>
        public event EventHandler<ResumenVenta>? VentaElegida;

        public ListadoVentasPorDia(VentaServicio ventas, ExportadorDeReportes exportador)
        {
            _ventas = ventas;
            _exportador = exportador;
            Font = Estilos.Normal;

            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            arriba.Controls.Add(new Label { Text = "Día:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            arriba.Controls.Add(dtpDia);
            arriba.Controls.Add(Estilos.Boton("Hoy", (_, _) => dtpDia.Value = DateTime.Today, 80));
            arriba.Controls.Add(Estilos.Boton("Ver detalle / anular (Enter)", (_, _) => Elegir(), 260));
            arriba.Controls.Add(Estilos.Boton("Exportar PDF…", (_, _) => ExportarPdf(), 160));
            arriba.Controls.Add(lblEstado);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(ResumenVenta.Id), "Nº", "N0", 80);
            Estilos.Columna(grilla, nameof(ResumenVenta.Fecha), "Hora", "HH:mm", 90);
            Estilos.Columna(grilla, nameof(ResumenVenta.Unidades), "Unidades", "N0", 100);
            Estilos.Columna(grilla, nameof(ResumenVenta.MedioDePagoDescripcion), "Medio de pago", llenar: true);
            Estilos.Columna(grilla, nameof(ResumenVenta.Estado), "Estado", ancho: 110);
            Estilos.Columna(grilla, nameof(ResumenVenta.Total), "Total", "C2", 150);

            // El estilo se aplica al formatear cada celda (no al pintar la fila): cambiar el estilo
            // de una fila mientras se pinta la vuelve a invalidar y la grilla queda repintándose sin fin.
            grilla.CellFormatting += (_, e) =>
            {
                if (e.RowIndex >= 0 && grilla.Rows[e.RowIndex].DataBoundItem is ResumenVenta { Anulada: true })
                {
                    e.CellStyle!.ForeColor = SystemColors.GrayText;
                    e.CellStyle.Font = Estilos.Tachada;
                }
            };
            grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Elegir(); };

            Controls.Add(grilla);
            Controls.Add(lblResumen);
            Controls.Add(arriba);

            dtpDia.ValueChanged += (_, _) => { lblEstado.Text = ""; Cargar(); };
        }

        public void Cargar()
        {
            var seleccionada = (grilla.CurrentRow?.DataBoundItem as ResumenVenta)?.Id;
            var resumen = _ventas.ResumenDelDia(DateOnly.FromDateTime(dtpDia.Value));
            var lista = resumen.Ventas.ToList();
            grilla.DataSource = lista;

            var indice = lista.FindIndex(v => v.Id == seleccionada);
            if (indice >= 0)
                grilla.CurrentCell = grilla.Rows[indice].Cells[0];

            var porMedio = string.Join("     ",
                resumen.TotalPorMedio.Select(par => $"{par.Key.Descripcion()}: {par.Value:C2}"));
            var anuladas = resumen.CantidadAnuladas > 0 ? $"  ({resumen.CantidadAnuladas} anuladas, no suman)" : "";

            lblResumen.Text = $"{resumen.CantidadVigentes} ventas  ·  TOTAL {resumen.Total:C2}{anuladas}" +
                              (porMedio == "" ? "" : $"\n{porMedio}");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && grilla.Focused)
            {
                Elegir();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Elegir()
        {
            if (grilla.CurrentRow?.DataBoundItem is ResumenVenta venta)
                VentaElegida?.Invoke(this, venta);
        }

        private void ExportarPdf()
        {
            var dia = DateOnly.FromDateTime(dtpDia.Value);
            Directory.CreateDirectory(ExportadorDeReportes.CarpetaPorDefecto);
            using var dialogo = new SaveFileDialog
            {
                Title = "Guardar las ventas del día en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                InitialDirectory = ExportadorDeReportes.CarpetaPorDefecto,
                FileName = ExportadorDeReportes.NombreDeArchivo(dia)
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var ruta = _exportador.GuardarVentasDelDia(dia, dialogo.FileName);
                lblEstado.Text = $"PDF guardado: {Path.GetFileName(ruta)}";
                lblEstado.ForeColor = Estilos.Exito;
                Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Lo más común: el PDF está abierto en otro programa y no se puede reemplazar.
                lblEstado.Text = $"No se pudo guardar el PDF: {ex.Message}";
                lblEstado.ForeColor = Color.Red;
            }
        }
    }
}
