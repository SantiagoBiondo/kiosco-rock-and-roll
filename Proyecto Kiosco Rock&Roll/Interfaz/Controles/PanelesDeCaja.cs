using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Base de los paneles de caja: un título, campos, un botón para confirmar y otro para cancelar,
    /// y una línea para mensajes. Enter confirma, Esc cancela.
    /// </summary>
    public abstract class PanelDeCaja : UserControl
    {
        private readonly Label lblMensaje = new() { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 8, 3, 3) };
        protected readonly TableLayoutPanel tabla = new() { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };

        public event EventHandler? Cancelado;

        protected PanelDeCaja(string titulo, string textoConfirmar, CajaServicio caja)
        {
            Caja = caja;
            Font = Estilos.Normal;
            tabla.Controls.Add(Estilos.Etiqueta(titulo, Estilos.Titulo));
            Controls.Add(tabla);
            TextoConfirmar = textoConfirmar;
        }

        protected CajaServicio Caja { get; }
        private string TextoConfirmar { get; }

        /// <summary>Lo que pasa al confirmar. Las reglas de negocio que fallen se muestran como mensaje.</summary>
        protected abstract void Confirmar();

        /// <summary>Agrega los botones y la línea de mensajes. Las subclases lo llaman al final de su constructor.</summary>
        protected void TerminarDeArmar()
        {
            var botones = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var btnConfirmar = Estilos.Boton($"{TextoConfirmar} (Enter)", (_, _) => IntentarConfirmar(), 200);
            btnConfirmar.BackColor = Estilos.Exito;
            btnConfirmar.ForeColor = Color.White;
            botones.Controls.Add(btnConfirmar);
            botones.Controls.Add(Estilos.Boton("Cancelar (Esc)", (_, _) => Cancelado?.Invoke(this, EventArgs.Empty), 150));
            tabla.Controls.Add(botones);
            tabla.Controls.Add(lblMensaje);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: IntentarConfirmar(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected void MostrarMensaje(string texto, Color color)
        {
            lblMensaje.Text = texto;
            lblMensaje.ForeColor = color;
        }

        protected static NumericUpDown CampoMonto()
        {
            var campo = Estilos.CampoNumero(2);
            campo.Font = Estilos.Grande;
            return campo;
        }

        /// <summary>Deja el campo vacío y con el cursor, listo para tipear.</summary>
        protected static void Preparar(NumericUpDown campo)
        {
            campo.Value = 0;
            campo.Text = "";
            campo.Focus();
        }

        private void IntentarConfirmar()
        {
            try
            {
                Confirmar();
            }
            catch (ReglaDeNegocioException ex)
            {
                MostrarMensaje(ex.Message, Color.Red);
            }
        }
    }

    /// <summary>Abrir la caja con el efectivo inicial (el cambio con el que se arranca).</summary>
    public sealed class PanelAbrirCaja : PanelDeCaja
    {
        private readonly NumericUpDown numInicial = CampoMonto();

        public event EventHandler<Turno>? Abierta;

        public PanelAbrirCaja(CajaServicio caja) : base("Abrir caja", "Abrir caja", caja)
        {
            tabla.Controls.Add(Estilos.Etiqueta("¿Con cuánto efectivo arrancás? (el cambio)"));
            tabla.Controls.Add(numInicial);
            TerminarDeArmar();
        }

        /// <param name="motivo">Texto opcional para explicar por qué se pide (por ejemplo, al querer cobrar).</param>
        public void Iniciar(string motivo = "")
        {
            MostrarMensaje(motivo, Estilos.Advertencia);
            Preparar(numInicial);
        }

        protected override void Confirmar() => Abierta?.Invoke(this, Caja.AbrirCaja(numInicial.ValorTipeado()));
    }

    /// <summary>Registrar un ingreso o un retiro de efectivo.</summary>
    public sealed class PanelMovimientoCaja : PanelDeCaja
    {
        private readonly ComboBox cmbTipo = Estilos.Opciones(Enum.GetValues<TipoMovimientoCaja>(), DescribirTipo, 380);
        private readonly NumericUpDown numMonto = CampoMonto();
        private readonly TextBox txtMotivo = new() { Width = 380, MaxLength = MovimientoCaja.LargoMaximoMotivo };

        public event EventHandler<MovimientoCaja>? Registrado;

        public PanelMovimientoCaja(CajaServicio caja) : base("Movimiento de efectivo", "Registrar", caja)
        {
            tabla.Controls.Add(Estilos.Etiqueta("Tipo"));
            tabla.Controls.Add(cmbTipo);
            tabla.Controls.Add(Estilos.Etiqueta("Monto"));
            tabla.Controls.Add(numMonto);
            tabla.Controls.Add(Estilos.Etiqueta("Motivo"));
            tabla.Controls.Add(txtMotivo);
            TerminarDeArmar();
        }

        private static string DescribirTipo(TipoMovimientoCaja tipo) => tipo switch
        {
            TipoMovimientoCaja.Ingreso => "Ingreso: entra efectivo a la caja",
            _ => "Retiro: sale efectivo de la caja"
        };

        public void Iniciar(TipoMovimientoCaja tipo)
        {
            cmbTipo.SelectedItem = tipo;
            txtMotivo.Text = "";
            txtMotivo.PlaceholderText = tipo == TipoMovimientoCaja.Retiro
                ? "Ej.: pago a proveedor, retiro del dueño"
                : "Ej.: cambio que traje";
            MostrarMensaje("", SystemColors.ControlText);
            Preparar(numMonto);
        }

        protected override void Confirmar()
        {
            var movimiento = Caja.RegistrarMovimiento((TipoMovimientoCaja)cmbTipo.SelectedItem!, numMonto.ValorTipeado(), txtMotivo.Text);
            Registrado?.Invoke(this, movimiento);
        }
    }

    /// <summary>Cerrar la caja: se cuenta el efectivo y se ve la diferencia con lo esperado.</summary>
    public sealed class PanelCerrarCaja : PanelDeCaja
    {
        private ResumenTurno? _resumen;

        private readonly Label lblEsperado = Estilos.Etiqueta("", Estilos.Grande);
        private readonly NumericUpDown numContado = CampoMonto();
        private readonly Label lblDiferencia = Estilos.Etiqueta("", Estilos.Grande);

        public event EventHandler<ResumenTurno>? Cerrada;

        public PanelCerrarCaja(CajaServicio caja) : base("Cerrar caja", "Cerrar caja", caja)
        {
            tabla.Controls.Add(Estilos.Etiqueta("Efectivo que debería haber"));
            tabla.Controls.Add(lblEsperado);
            tabla.Controls.Add(Estilos.Etiqueta("Contá el efectivo de la caja y escribilo acá"));
            tabla.Controls.Add(numContado);
            tabla.Controls.Add(Estilos.Etiqueta("Diferencia"));
            tabla.Controls.Add(lblDiferencia);
            TerminarDeArmar();

            numContado.ValueChanged += (_, _) => ActualizarDiferencia();
            numContado.KeyUp += (_, _) => ActualizarDiferencia();
        }

        public void Iniciar()
        {
            var turno = Caja.TurnoAbierto;
            _resumen = turno is null ? null : Caja.Resumen(turno);
            lblEsperado.Text = _resumen?.EfectivoEsperado.ToString("C2") ?? "-";
            MostrarMensaje(_resumen is null ? "La caja ya está cerrada." : "", Estilos.Advertencia);
            Preparar(numContado);
            ActualizarDiferencia();
        }

        private bool HayAlgoTipeado => numContado.Text.Trim() != "";

        private void ActualizarDiferencia()
        {
            if (_resumen is null || !HayAlgoTipeado)
            {
                lblDiferencia.Text = "-";
                lblDiferencia.ForeColor = SystemColors.ControlText;
                return;
            }

            var diferencia = _resumen.DiferenciaSiSeContara(numContado.ValorTipeado());
            lblDiferencia.Text = ResumenTurno.DescribirDiferencia(diferencia);
            lblDiferencia.ForeColor = diferencia == 0 ? Estilos.Exito : diferencia > 0 ? Estilos.Advertencia : Color.Red;
        }

        protected override void Confirmar()
        {
            if (!HayAlgoTipeado)
            {
                MostrarMensaje("Escribí cuánto efectivo contaste (si no hay nada, poné 0).", Estilos.Advertencia);
                return;
            }

            var contado = numContado.ValorTipeado();
            if (_resumen is { } resumen && resumen.DiferenciaSiSeContara(contado) != 0)
            {
                var diferencia = ResumenTurno.DescribirDiferencia(resumen.DiferenciaSiSeContara(contado));
                if (!Mensajes.Confirmar(this, $"{diferencia}. ¿Cerrar la caja igual?"))
                    return;
            }

            Cerrada?.Invoke(this, Caja.CerrarCaja(contado));
        }
    }
}
