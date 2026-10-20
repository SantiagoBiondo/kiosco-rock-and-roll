using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>Elegir medio de pago y, si es efectivo, ver el vuelto. Enter confirma, Esc cancela.</summary>
    public class PanelCobro : UserControl
    {
        private decimal _total;

        private readonly Label lblTotal = Estilos.Etiqueta("", Estilos.Total);
        private readonly ComboBox cmbMedio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, Font = Estilos.Grande };
        private readonly NumericUpDown numPagaCon = Estilos.CampoNumero(2);
        private readonly Label lblVuelto = Estilos.Etiqueta("", Estilos.Grande);
        private readonly Label lblError = new() { AutoSize = true, ForeColor = Color.Red, MaximumSize = new Size(400, 0) };

        public event EventHandler<Cobro>? Confirmado;
        public event EventHandler? Cancelado;

        public PanelCobro()
        {
            Font = Estilos.Normal;
            numPagaCon.Font = Estilos.Grande;
            cmbMedio.DataSource = Enum.GetValues<MedioDePago>();
            cmbMedio.Format += (_, e) => e.Value = ((MedioDePago)e.ListItem!).Descripcion();

            var tabla = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
            tabla.Controls.Add(Estilos.Etiqueta("Cobrar", Estilos.Titulo));
            tabla.Controls.Add(lblTotal);
            tabla.Controls.Add(Estilos.Etiqueta("Medio de pago (↑ ↓ para cambiar)"));
            tabla.Controls.Add(cmbMedio);
            tabla.Controls.Add(Estilos.Etiqueta("Paga con (vacío = justo)"));
            tabla.Controls.Add(numPagaCon);
            tabla.Controls.Add(Estilos.Etiqueta("Vuelto"));
            tabla.Controls.Add(lblVuelto);

            var botones = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var btnConfirmar = Estilos.Boton("Confirmar (Enter)", (_, _) => Confirmar(), 180);
            btnConfirmar.BackColor = Estilos.Exito;
            btnConfirmar.ForeColor = Color.White;
            botones.Controls.Add(btnConfirmar);
            botones.Controls.Add(Estilos.Boton("Volver (Esc)", (_, _) => Cancelado?.Invoke(this, EventArgs.Empty), 150));
            tabla.Controls.Add(botones);
            tabla.Controls.Add(lblError);

            Controls.Add(tabla);

            cmbMedio.SelectedIndexChanged += (_, _) => ActualizarVuelto();
            numPagaCon.ValueChanged += (_, _) => ActualizarVuelto();
            numPagaCon.KeyUp += (_, _) => ActualizarVuelto();
        }

        public void Iniciar(decimal total)
        {
            _total = total;
            lblTotal.Text = total.ToString("C2");
            cmbMedio.SelectedItem = MedioDePago.Efectivo;
            numPagaCon.Value = 0;
            numPagaCon.Text = "";
            lblError.Text = "";
            ActualizarVuelto();
            numPagaCon.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Confirmar(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
                // Flechas: cambian el medio de pago aunque el cursor esté en "Paga con".
                case Keys.Up or Keys.Down when !cmbMedio.Focused:
                    var indice = cmbMedio.SelectedIndex + (keyData == Keys.Down ? 1 : -1);
                    cmbMedio.SelectedIndex = Math.Clamp(indice, 0, cmbMedio.Items.Count - 1);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private MedioDePago MedioElegido => (MedioDePago)cmbMedio.SelectedItem!;

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

            var pagaCon = numPagaCon.ValorTipeado();
            var vuelto = pagaCon == 0 ? 0 : pagaCon - _total;
            lblVuelto.Text = vuelto >= 0 ? vuelto.ToString("C2") : $"Faltan {-vuelto:C2}";
            lblVuelto.ForeColor = vuelto >= 0 ? Estilos.Exito : Color.Red;
        }

        private void Confirmar()
        {
            try
            {
                var cobro = Cobro.Crear(_total, MedioElegido, numPagaCon.ValorTipeado());
                lblError.Text = "";
                Confirmado?.Invoke(this, cobro);
            }
            catch (ReglaDeNegocioException ex)
            {
                lblError.Text = ex.Message;
            }
        }
    }
}
