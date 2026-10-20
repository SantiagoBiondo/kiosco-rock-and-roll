using System.Globalization;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>Elegir medio de pago y, si es efectivo, mostrar el vuelto.</summary>
    public class FormCobro : Form
    {
        private readonly decimal _total;
        private readonly ComboBox cmbMedio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, Font = Estilos.Grande };
        private readonly NumericUpDown numPagaCon = Estilos.CampoNumero(2);
        private readonly Label lblVuelto = new() { AutoSize = true, Font = Estilos.Grande };

        public Cobro? Resultado { get; private set; }

        public FormCobro(decimal total)
        {
            _total = total;

            Text = "Cobrar";
            Font = Estilos.Normal;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16);

            numPagaCon.Font = Estilos.Grande;
            cmbMedio.DataSource = Enum.GetValues<MedioDePago>();
            cmbMedio.Format += (_, e) => e.Value = ((MedioDePago)e.ListItem!).Descripcion();

            var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            tabla.Controls.Add(new Label { Text = "Total:", AutoSize = true, Font = Estilos.Grande, Anchor = AnchorStyles.Left });
            tabla.Controls.Add(new Label { Text = total.ToString("C2"), AutoSize = true, Font = Estilos.Total });
            tabla.Controls.Add(new Label { Text = "Medio de pago:", AutoSize = true, Anchor = AnchorStyles.Left });
            tabla.Controls.Add(cmbMedio);
            tabla.Controls.Add(new Label { Text = "Paga con:", AutoSize = true, Anchor = AnchorStyles.Left });
            tabla.Controls.Add(numPagaCon);
            tabla.Controls.Add(new Label { Text = "Vuelto:", AutoSize = true, Anchor = AnchorStyles.Left });
            tabla.Controls.Add(lblVuelto);

            var btnConfirmar = Estilos.Boton("Confirmar (Enter)", (_, _) => Confirmar());
            var btnCancelar = Estilos.Boton("Cancelar (Esc)", (_, _) => Close());
            btnCancelar.DialogResult = DialogResult.Cancel;
            var botones = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            botones.Controls.AddRange([btnCancelar, btnConfirmar]);
            tabla.Controls.Add(botones);
            tabla.SetColumnSpan(botones, 2);

            Controls.Add(tabla);
            AcceptButton = btnConfirmar;
            CancelButton = btnCancelar;

            cmbMedio.SelectedIndexChanged += (_, _) => ActualizarVuelto();
            numPagaCon.ValueChanged += (_, _) => ActualizarVuelto();
            numPagaCon.KeyUp += (_, _) => ActualizarVuelto();
            Shown += (_, _) => { numPagaCon.Focus(); numPagaCon.Select(0, numPagaCon.Text.Length); };
            ActualizarVuelto();
        }

        private MedioDePago MedioElegido => (MedioDePago)cmbMedio.SelectedItem!;

        // Se lee el texto (y no Value) para que el vuelto se actualice mientras se tipea.
        private decimal MontoTipeado =>
            decimal.TryParse(numPagaCon.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var monto) ? monto : 0;

        private void ActualizarVuelto()
        {
            var esEfectivo = MedioElegido == MedioDePago.Efectivo;
            numPagaCon.Enabled = esEfectivo;
            if (!esEfectivo)
            {
                lblVuelto.Text = "-";
                lblVuelto.ForeColor = SystemColors.ControlText;
                return;
            }

            var vuelto = MontoTipeado == 0 ? 0 : MontoTipeado - _total;
            lblVuelto.Text = vuelto >= 0 ? vuelto.ToString("C2") : $"Faltan {-vuelto:C2}";
            lblVuelto.ForeColor = vuelto >= 0 ? Estilos.Exito : Color.Red;
        }

        private void Confirmar()
        {
            try
            {
                Resultado = Cobro.Crear(_total, MedioElegido, MontoTipeado);
                DialogResult = DialogResult.OK;
            }
            catch (ReglaDeNegocioException ex)
            {
                Mensajes.Advertencia(this, ex.Message);
            }
        }
    }
}
