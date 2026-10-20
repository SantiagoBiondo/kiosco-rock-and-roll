using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>Ventas de un día con totales por medio de pago (sirve para el cierre de caja).</summary>
    public class FormVentasDelDia : Form
    {
        private readonly VentaServicio _ventas;

        private readonly DateTimePicker dtpDia = new() { Format = DateTimePickerFormat.Long, Width = 300 };
        private readonly DataGridView grilla = new();
        private readonly Label lblResumen = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), Font = Estilos.Grande };

        public FormVentasDelDia(VentaServicio ventas)
        {
            _ventas = ventas;

            Text = "Ventas del día";
            Font = Estilos.Normal;
            Size = new Size(850, 600);
            StartPosition = FormStartPosition.CenterParent;

            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            arriba.Controls.Add(new Label { Text = "Día:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            arriba.Controls.Add(dtpDia);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(ResumenVenta.Id), "Nº", "N0", 80);
            Estilos.Columna(grilla, nameof(ResumenVenta.Fecha), "Hora", "HH:mm", 90);
            Estilos.Columna(grilla, nameof(ResumenVenta.Unidades), "Unidades", "N0", 100);
            Estilos.Columna(grilla, nameof(ResumenVenta.MedioDePagoDescripcion), "Medio de pago", llenar: true);
            Estilos.Columna(grilla, nameof(ResumenVenta.Total), "Total", "C2", 150);

            Controls.Add(grilla);
            Controls.Add(lblResumen);
            Controls.Add(arriba);

            dtpDia.ValueChanged += (_, _) => Cargar();
            Cargar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Cargar()
        {
            var resumen = _ventas.ResumenDelDia(DateOnly.FromDateTime(dtpDia.Value));
            grilla.DataSource = resumen.Ventas.ToList();

            var porMedio = string.Join("     ",
                resumen.TotalPorMedio.Select(par => $"{par.Key.Descripcion()}: {par.Value:C2}"));

            lblResumen.Text = $"{resumen.Ventas.Count} ventas  ·  TOTAL {resumen.Total:C2}" +
                              (porMedio == "" ? "" : $"\n{porMedio}");
        }
    }
}
