using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using KioscoRockAndRoll.Reportes;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Impresion;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Detalle de una venta (qué se llevó, a qué precio) con la opción de anularla.
    /// Enter anula (pide confirmación), Esc cierra.
    /// </summary>
    public class PanelDetalleVenta : UserControl
    {
        private readonly VentaServicio _ventas;
        private readonly ImpresoraDeTickets _impresora;
        private readonly Button btnImprimir;
        private DetalleVenta? _detalle;

        private readonly Label lblTitulo = Estilos.Etiqueta("", Estilos.Titulo);
        private readonly Label lblDatos = new() { AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(3, 4, 3, 8) };
        private readonly DataGridView grilla = new();
        private readonly Label lblTotal = new() { AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 8, 3, 8) };
        private readonly Panel zonaAnular = new() { Dock = DockStyle.Bottom, AutoSize = true };
        private readonly TextBox txtMotivo = new() { Width = 420, MaxLength = ResumenVenta.LargoMaximoMotivo, PlaceholderText = "Ej.: cobré de más, el cliente devolvió el producto" };
        private readonly Label lblMensaje = new() { AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(3, 8, 3, 3) };

        /// <summary>Se anuló la venta.</summary>
        public event EventHandler? Anulada;

        public event EventHandler? Cerrado;

        public PanelDetalleVenta(VentaServicio ventas, ImpresoraDeTickets impresora)
        {
            _ventas = ventas;
            _impresora = impresora;
            Font = Estilos.Normal;

            var arriba = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
            arriba.Controls.Add(lblTitulo);
            arriba.Controls.Add(lblDatos);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            grilla.TabStop = false;
            Estilos.Columna(grilla, nameof(LineaVenta.NombreProducto), "Producto", llenar: true);
            Estilos.Columna(grilla, nameof(LineaVenta.Cantidad), "Cant.", "N0", 60);
            Estilos.Columna(grilla, nameof(LineaVenta.PrecioUnitario), "Precio", "C2", 105);
            Estilos.Columna(grilla, nameof(LineaVenta.Subtotal), "Subtotal", "C2", 115);

            var abajo = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            var anular = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
            anular.Controls.Add(Estilos.Etiqueta("Motivo de la anulación"));
            anular.Controls.Add(txtMotivo);
            var btnAnular = Estilos.Boton("Anular venta (Enter)", (_, _) => AnularVenta(), 200);
            Estilos.Destacar(btnAnular, Estilos.FondoBotonPeligro);
            anular.Controls.Add(btnAnular);
            zonaAnular.Controls.Add(anular);

            abajo.Controls.Add(lblTotal);
            abajo.Controls.Add(zonaAnular);
            var botones = new FlowLayoutPanel { AutoSize = true };
            btnImprimir = Estilos.Boton("Imprimir ticket", (_, _) => ImprimirCopia(), 170);
            botones.Controls.Add(btnImprimir);
            botones.Controls.Add(Estilos.Boton("Cerrar (Esc)", (_, _) => Cerrado?.Invoke(this, EventArgs.Empty), 150));
            abajo.Controls.Add(botones);
            abajo.Controls.Add(lblMensaje);
            var pie = new Panel { Dock = DockStyle.Bottom, AutoSize = true };
            pie.Controls.Add(abajo);

            Controls.Add(grilla);
            Controls.Add(pie);
            Controls.Add(arriba);
        }

        public void Mostrar(long ventaId)
        {
            _detalle = _ventas.Detalle(ventaId);
            txtMotivo.Text = "";
            MostrarMensaje("", SystemColors.ControlText);

            if (_detalle is not { Venta: var venta })
            {
                lblTitulo.Text = "La venta no existe";
                lblDatos.Text = "";
                grilla.DataSource = null;
                lblTotal.Text = "";
                zonaAnular.Visible = false;
                btnImprimir.Visible = false;
                return;
            }

            // Una venta anulada no se reimprime; sin impresora configurada, el botón no tiene sentido.
            btnImprimir.Visible = !venta.Anulada && _impresora.Configuracion.HayImpresora;

            lblTitulo.Text = $"Venta Nº {venta.Id}";
            lblDatos.Text = $"{venta.Fecha:dd/MM/yyyy HH:mm}  ·  {venta.MedioDePagoDescripcion}";
            if (venta.Anulada)
            {
                lblDatos.Text += $"\nANULADA el {venta.AnuladaEn:dd/MM HH:mm}. Motivo: {venta.MotivoAnulacion}";
                lblDatos.ForeColor = Estilos.Error;
            }
            else
            {
                lblDatos.ForeColor = SystemColors.ControlText;
            }

            grilla.DataSource = _detalle.Lineas.ToList();
            lblTotal.Text = $"Total: {venta.Total:C2}";

            // Si no se puede anular, se explica enseguida en vez de dejar escribir el motivo en vano.
            var impedimento = _ventas.PorQueNoSePuedeAnular(venta);
            zonaAnular.Visible = impedimento is null;
            if (impedimento is not null && !venta.Anulada)
                MostrarMensaje(impedimento, SystemColors.GrayText);
            if (zonaAnular.Visible)
                txtMotivo.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter when zonaAnular.Visible: AnularVenta(); return true;
                case Keys.Escape: Cerrado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AnularVenta()
        {
            if (_detalle is not { Venta: var venta })
                return;

            var pregunta = $"¿Anular la venta Nº {venta.Id} por {venta.Total:C2}?\n\n" +
                           "El stock de los productos vuelve y la venta deja de contar en la caja.";
            try
            {
                // Se valida antes de preguntar, para no pedir confirmación de algo que no se puede hacer.
                if (txtMotivo.Text.Trim() == "")
                    throw new ReglaDeNegocioException("Escribí el motivo de la anulación (por ejemplo: \"cobré de más\").");
                if (!Mensajes.Confirmar(this, pregunta))
                    return;

                _ventas.Anular(venta.Id, txtMotivo.Text);
            }
            catch (ReglaDeNegocioException ex)
            {
                MostrarMensaje(ex.Message, Estilos.Error);
                return;
            }

            Anulada?.Invoke(this, EventArgs.Empty);
        }

        private void ImprimirCopia()
        {
            if (_detalle is null)
                return;
            try
            {
                _impresora.Imprimir(Ticket.CopiaDe(_detalle, ExportadorDeReportes.NombreNegocio));
                MostrarMensaje("Se mandó la copia del ticket a la impresora.", Estilos.Exito);
            }
            catch (Exception ex) when (ex is NoSePudoImprimirException or ReglaDeNegocioException)
            {
                MostrarMensaje(ex.Message, Estilos.Error);
            }
        }

        private void MostrarMensaje(string texto, Color color)
        {
            lblMensaje.Text = texto;
            lblMensaje.ForeColor = color;
        }
    }
}
