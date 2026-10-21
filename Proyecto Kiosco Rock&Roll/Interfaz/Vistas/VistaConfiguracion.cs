using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Entidades;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Impresion;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Configuración de la impresora de tickets: cuál usar, de qué ancho es el papel y si
    /// se imprime siempre al cobrar. Los cambios se guardan apenas se hacen.
    /// </summary>
    public class VistaConfiguracion : Vista
    {
        private const string Ninguna = "(ninguna: no imprimir tickets)";

        private readonly AjustesDeTicket _ajustes;
        private bool _cargando;

        private readonly ComboBox cmbImpresora = Estilos.Opciones<Opcion<string?>>([], o => o.Texto, 420);
        private readonly ComboBox cmbPapel = Estilos.Opciones(Enum.GetValues<AnchoDePapel>(),
            a => a == AnchoDePapel.Rollo80mm ? "Rollo de 80 mm" : "Rollo de 58 mm", 200);
        private readonly CheckBox chkImprimirSiempre = new()
        {
            Text = "Imprimir el ticket siempre al cobrar (si no, se marca la casilla cuando el cliente lo pide)",
            AutoSize = true,
            Margin = new Padding(3, 12, 3, 3)
        };
        private readonly Label lblEstado = new() { AutoSize = true, Margin = new Padding(3, 12, 3, 3), MaximumSize = new Size(700, 0) };
        private readonly TextBox txtVistaPrevia = new()
        {
            Multiline = true,
            ReadOnly = true,
            Font = new Font("Consolas", 11F),
            BackColor = Color.White,
            ScrollBars = ScrollBars.Vertical,
            Width = 520,
            Height = 480,
            TabStop = false
        };

        public VistaConfiguracion(AjustesDeTicket ajustes)
        {
            _ajustes = ajustes;

            var opciones = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Left, Padding = new Padding(16) };
            opciones.Controls.Add(Estilos.Etiqueta("Impresora de tickets", Estilos.Titulo));
            opciones.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                Text = "Elegí por qué impresora salen los tickets. Sirve cualquier impresora instalada en Windows: " +
                       "una de tickets (térmica) o una común. Los cambios se guardan solos."
            });

            opciones.Controls.Add(Estilos.Etiqueta("Impresora"));
            var filaImpresora = new FlowLayoutPanel { AutoSize = true };
            filaImpresora.Controls.Add(cmbImpresora);
            filaImpresora.Controls.Add(Estilos.Boton("Actualizar lista", (_, _) => CargarImpresoras(), 160));
            opciones.Controls.Add(filaImpresora);

            opciones.Controls.Add(Estilos.Etiqueta("Ancho del papel"));
            opciones.Controls.Add(cmbPapel);
            opciones.Controls.Add(chkImprimirSiempre);

            var btnPrueba = Estilos.Boton("Imprimir ticket de prueba", (_, _) => ImprimirPrueba(), 240);
            btnPrueba.Margin = new Padding(3, 16, 3, 3);
            opciones.Controls.Add(btnPrueba);
            opciones.Controls.Add(lblEstado);

            var vistaPrevia = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Left, Padding = new Padding(16) };
            vistaPrevia.Controls.Add(Estilos.Etiqueta("Así sale el ticket", Estilos.Titulo));
            vistaPrevia.Controls.Add(txtVistaPrevia);

            // El orden importa con Dock = Left: lo último que se agrega queda más a la izquierda.
            Controls.Add(vistaPrevia);
            Controls.Add(opciones);

            cmbImpresora.SelectedIndexChanged += (_, _) => Guardar();
            cmbPapel.SelectedIndexChanged += (_, _) => Guardar();
            chkImprimirSiempre.CheckedChanged += (_, _) => Guardar();
        }

        public override string Titulo => "Configuración";

        public override void AlMostrar()
        {
            CargarImpresoras();
            cmbImpresora.Focus();
        }

        /// <summary>Lista las impresoras de Windows y muestra la configuración guardada.</summary>
        private void CargarImpresoras()
        {
            var configuracion = _ajustes.Leer();
            var instaladas = ImpresoraDeTickets.ImpresorasInstaladas();

            var opciones = new List<Opcion<string?>> { new(null, Ninguna) };
            opciones.AddRange(instaladas.Select(i => new Opcion<string?>(i, i)));
            // La elegida puede no estar si está desenchufada: se muestra igual, para no perder la configuración.
            if (configuracion.HayImpresora && !instaladas.Contains(configuracion.Impresora))
                opciones.Add(new Opcion<string?>(configuracion.Impresora, $"{configuracion.Impresora} (no encontrada)"));

            _cargando = true;
            Estilos.CambiarOpciones(cmbImpresora, opciones);
            Estilos.Elegir<Opcion<string?>>(cmbImpresora, o => o.Valor == configuracion.Impresora);
            cmbPapel.SelectedItem = configuracion.Ancho;
            chkImprimirSiempre.Checked = configuracion.ImprimirAlCobrar;
            _cargando = false;

            MostrarVistaPrevia(configuracion);
            MostrarEstado(configuracion.HayImpresora ? "" : "Todavía no hay impresora elegida: no se imprimen tickets.",
                SystemColors.GrayText);
        }

        private ConfiguracionDeTicket ConfiguracionEnPantalla => new(
            (cmbImpresora.SelectedItem as Opcion<string?>)?.Valor,
            cmbPapel.SelectedItem is AnchoDePapel ancho ? ancho : AnchoDePapel.Rollo58mm,
            chkImprimirSiempre.Checked);

        private void Guardar()
        {
            if (_cargando)
                return;

            var configuracion = ConfiguracionEnPantalla;
            _ajustes.Guardar(configuracion);
            chkImprimirSiempre.Enabled = configuracion.HayImpresora;
            MostrarVistaPrevia(configuracion);
            MostrarEstado("Guardado.", Estilos.Exito);
        }

        private void MostrarVistaPrevia(ConfiguracionDeTicket configuracion)
        {
            chkImprimirSiempre.Enabled = configuracion.HayImpresora;
            txtVistaPrevia.Text = ImpresoraDeTickets.VistaPrevia(ImpresoraDeTickets.TicketDePrueba(), configuracion);
        }

        private void ImprimirPrueba()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                ImpresoraDeTickets.Imprimir(ImpresoraDeTickets.TicketDePrueba(), ConfiguracionEnPantalla);
                MostrarEstado("Se mandó el ticket de prueba a la impresora.", Estilos.Exito);
            }
            catch (NoSePudoImprimirException ex)
            {
                MostrarEstado(ex.Message, Color.Red);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
