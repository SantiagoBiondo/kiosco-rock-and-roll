using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Elegir cómo se paga y, si hay efectivo, ver el vuelto. Se puede pagar con un medio o con
    /// "Varios medios": se cargan los montos con tarjeta o transferencia y el resto va en efectivo.
    /// Enter confirma, Esc cancela.
    /// </summary>
    public class PanelCobro : UserControl
    {
        // Null = "Varios medios".
        private static readonly Opcion<MedioDePago?>[] OpcionesDeMedio =
        [
            .. Enum.GetValues<MedioDePago>().Select(m => new Opcion<MedioDePago?>(m, m.Descripcion())),
            new(null, "Varios medios")
        ];

        private static readonly MedioDePago[] OtrosMedios = [MedioDePago.Debito, MedioDePago.Credito, MedioDePago.TransferenciaQr];

        private decimal _total;

        private readonly Label lblTotal = Estilos.Etiqueta("", Estilos.Total);
        private readonly ComboBox cmbMedio = Estilos.Opciones(OpcionesDeMedio, o => o.Texto, 250);
        private readonly TableLayoutPanel zonaVarios = new() { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        private readonly Dictionary<MedioDePago, NumericUpDown> montosOtrosMedios = [];
        private readonly Label lblResto = Estilos.Etiqueta("", Estilos.Grande);
        private readonly Label lblPagaCon = Estilos.Etiqueta("Paga con (vacío = justo)");
        private readonly NumericUpDown numPagaCon = Estilos.CampoNumero(2);
        private readonly Label lblVuelto = Estilos.Etiqueta("", Estilos.Grande);
        private readonly Label lblError = new() { AutoSize = true, ForeColor = Color.Red, MaximumSize = new Size(400, 0) };

        public event EventHandler<Cobro>? Confirmado;
        public event EventHandler? Cancelado;

        public PanelCobro()
        {
            Font = Estilos.Normal;
            numPagaCon.Font = Estilos.Grande;
            cmbMedio.Font = Estilos.Grande;

            // Varios medios: un monto por cada medio que no es efectivo; lo que falta, en efectivo.
            foreach (var medio in OtrosMedios)
            {
                var campo = Estilos.CampoNumero(2);
                campo.Width = 180;
                campo.ValueChanged += (_, _) => Actualizar();
                campo.KeyUp += (_, _) => Actualizar();
                montosOtrosMedios[medio] = campo;
                zonaVarios.Controls.Add(Estilos.Etiqueta(medio.Descripcion()));
                zonaVarios.Controls.Add(campo);
            }
            zonaVarios.Controls.Add(Estilos.Etiqueta("Resto en efectivo"));
            zonaVarios.Controls.Add(lblResto);

            var tabla = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
            tabla.Controls.Add(Estilos.Etiqueta("Cobrar", Estilos.Titulo));
            tabla.Controls.Add(lblTotal);
            tabla.Controls.Add(Estilos.Etiqueta("Medio de pago (↑ ↓ para cambiar)"));
            tabla.Controls.Add(cmbMedio);
            tabla.Controls.Add(zonaVarios);
            tabla.Controls.Add(lblPagaCon);
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

            cmbMedio.SelectedIndexChanged += (_, _) => MedioCambiado();
            numPagaCon.ValueChanged += (_, _) => Actualizar();
            numPagaCon.KeyUp += (_, _) => Actualizar();
        }

        public void Iniciar(decimal total)
        {
            _total = total;
            lblTotal.Text = total.ToString("C2");
            Estilos.Elegir<Opcion<MedioDePago?>>(cmbMedio, o => o.Valor == MedioDePago.Efectivo);
            Vaciar(numPagaCon);
            lblError.Text = "";
            MedioCambiado();
            numPagaCon.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Confirmar(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
                // Flechas: cambian el medio de pago aunque el cursor esté en "Paga con".
                // Con "Varios medios" se dejan para los campos de montos (se vuelve al combo con Shift+Tab).
                case Keys.Up or Keys.Down when !cmbMedio.Focused && (!EsVarios || numPagaCon.Focused):
                    var indice = cmbMedio.SelectedIndex + (keyData == Keys.Down ? 1 : -1);
                    cmbMedio.SelectedIndex = Math.Clamp(indice, 0, cmbMedio.Items.Count - 1);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private MedioDePago? MedioElegido => (cmbMedio.SelectedItem as Opcion<MedioDePago?>)?.Valor;

        private bool EsVarios => cmbMedio.SelectedItem is Opcion<MedioDePago?> { Valor: null };

        private void MedioCambiado()
        {
            zonaVarios.Visible = EsVarios;
            if (EsVarios)
            {
                foreach (var campo in montosOtrosMedios.Values)
                    Vaciar(campo);
                Vaciar(numPagaCon);
                if (!cmbMedio.Focused)
                    montosOtrosMedios[MedioDePago.Debito].Focus();
            }
            Actualizar();
        }

        /// <summary>Arma el cobro con lo cargado en pantalla (puede lanzar ReglaDeNegocioException).</summary>
        private Cobro ArmarCobro()
        {
            if (!EsVarios)
                return Cobro.Crear(_total, MedioElegido!.Value, numPagaCon.ValorTipeado());

            var otros = montosOtrosMedios.Select(par => new Pago(par.Key, par.Value.ValorTipeado()));
            return Cobro.Combinado(_total, otros, numPagaCon.ValorTipeado());
        }

        /// <summary>Recalcula el resto en efectivo y el vuelto mientras se tipea.</summary>
        private void Actualizar()
        {
            var restoEnEfectivo = EsVarios
                ? _total - montosOtrosMedios.Values.Sum(c => c.ValorTipeado())
                : MedioElegido == MedioDePago.Efectivo ? _total : 0;

            lblResto.Text = restoEnEfectivo >= 0 ? restoEnEfectivo.ToString("C2") : $"Te pasaste {-restoEnEfectivo:C2}";
            lblResto.ForeColor = restoEnEfectivo >= 0 ? SystemColors.ControlText : Color.Red;

            var hayEfectivo = restoEnEfectivo > 0;
            numPagaCon.Enabled = lblPagaCon.Enabled = hayEfectivo;
            if (!hayEfectivo)
            {
                lblVuelto.Text = "-";
                lblVuelto.ForeColor = SystemColors.ControlText;
                return;
            }

            var pagaCon = numPagaCon.ValorTipeado();
            var vuelto = pagaCon == 0 ? 0 : pagaCon - restoEnEfectivo;
            lblVuelto.Text = vuelto >= 0 ? vuelto.ToString("C2") : $"Faltan {-vuelto:C2}";
            lblVuelto.ForeColor = vuelto >= 0 ? Estilos.Exito : Color.Red;
        }

        private void Confirmar()
        {
            try
            {
                var cobro = ArmarCobro();
                lblError.Text = "";
                Confirmado?.Invoke(this, cobro);
            }
            catch (ReglaDeNegocioException ex)
            {
                lblError.Text = ex.Message;
            }
        }

        private static void Vaciar(NumericUpDown campo)
        {
            campo.Value = 0;
            campo.Text = "";
        }
    }
}
