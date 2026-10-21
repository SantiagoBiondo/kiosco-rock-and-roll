using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Arma el pedido a un proveedor con sus productos de stock bajo. Las cantidades se pueden
    /// corregir y el texto se copia para pegarlo en WhatsApp o en un mail. Esc cierra.
    /// </summary>
    public class PanelPedido : UserControl
    {
        private const string NombreNegocio = "Kiosco Rock & Roll";

        private readonly InventarioServicio _inventario;
        private readonly CatalogoServicio<Proveedor> _proveedores;
        private readonly TimeProvider _reloj;
        private PedidoAProveedor? _pedido;

        private readonly ComboBox cmbProveedor = Estilos.Opciones<Proveedor>([], p => p.Nombre, 300);
        private readonly Label lblTelefono = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
        private readonly DataGridView grilla = new();
        private readonly Label lblMensaje = new() { AutoSize = true, MaximumSize = new Size(480, 0) };

        public event EventHandler? Cerrado;

        public PanelPedido(InventarioServicio inventario, CatalogoServicio<Proveedor> proveedores, TimeProvider reloj)
        {
            _inventario = inventario;
            _proveedores = proveedores;
            _reloj = reloj;
            Font = Estilos.Normal;

            var arriba = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top };
            arriba.Controls.Add(Estilos.Etiqueta("Pedido a proveedor", Estilos.Titulo));
            arriba.Controls.Add(cmbProveedor);
            arriba.Controls.Add(lblTelefono);
            arriba.Controls.Add(new Label
            {
                Text = "Productos con stock bajo. Corregí la columna \"Pedir\" si hace falta (0 = no pedir).",
                AutoSize = true,
                MaximumSize = new Size(480, 0),
                Margin = new Padding(3, 8, 3, 6)
            });

            Estilos.ConfigurarGrilla(grilla);
            Estilos.Columna(grilla, nameof(LineaPedido.Nombre), "Producto", llenar: true);
            Estilos.Columna(grilla, nameof(LineaPedido.Stock), "Stock", "N0", 65);
            Estilos.Columna(grilla, nameof(LineaPedido.StockMinimo), "Mín.", "N0", 60);
            var colPedir = Estilos.Columna(grilla, nameof(LineaPedido.Cantidad), "Pedir", "N0", 75, soloLectura: false);
            colPedir.DefaultCellStyle.Font = new Font(Estilos.Normal, FontStyle.Bold);
            grilla.DataError += (_, e) =>
            {
                e.Cancel = true;
                MostrarMensaje("La cantidad tiene que ser un número entero, 0 o más.", Estilos.Error);
            };

            var abajo = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Bottom };
            var botones = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            var btnCopiar = Estilos.Boton("Copiar pedido (Enter)", (_, _) => Copiar(), 210);
            Estilos.Destacar(btnCopiar, Estilos.FondoBotonPrincipal);
            botones.Controls.Add(btnCopiar);
            botones.Controls.Add(Estilos.Boton("Cerrar (Esc)", (_, _) => Cerrado?.Invoke(this, EventArgs.Empty), 150));
            abajo.Controls.Add(botones);
            abajo.Controls.Add(lblMensaje);

            Controls.Add(grilla);
            Controls.Add(abajo);
            Controls.Add(arriba);

            cmbProveedor.SelectedIndexChanged += (_, _) => Armar();
        }

        /// <param name="proveedorId">
        /// Proveedor a mostrar primero (por ejemplo, el del filtro del inventario). Si es null,
        /// se muestra el primero que tenga algo para pedir.
        /// </param>
        public void Activar(long? proveedorId)
        {
            var proveedores = _proveedores.Listar();
            Estilos.CambiarOpciones(cmbProveedor, proveedores);
            if (proveedores.Count == 0)
            {
                _pedido = null;
                grilla.DataSource = null;
                lblTelefono.Text = "";
                MostrarMensaje("Todavía no hay proveedores. Cargalos con el botón \"Proveedores\".", Estilos.Advertencia);
                return;
            }

            proveedorId ??= proveedores.FirstOrDefault(p => _inventario.ArmarPedido(p).Lineas.Count > 0)?.Id;
            Estilos.Elegir<Proveedor>(cmbProveedor, p => p.Id == proveedorId);
            Armar();
            cmbProveedor.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                // Copiar confirma primero la cantidad que se esté editando.
                case Keys.Enter: Copiar(); return true;
                case Keys.Escape when !grilla.IsCurrentCellInEditMode: Cerrado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Armar()
        {
            if (cmbProveedor.SelectedItem is not Proveedor proveedor)
                return;

            _pedido = _inventario.ArmarPedido(proveedor);
            grilla.DataSource = _pedido.Lineas.ToList();
            lblTelefono.Text = proveedor.Telefono == "" ? "Sin teléfono cargado" : $"Tel.: {proveedor.Telefono}";
            MostrarMensaje(_pedido.Lineas.Count == 0
                    ? "Este proveedor no tiene productos con stock bajo."
                    : $"{_pedido.Lineas.Count} productos para reponer.",
                SystemColors.ControlText);
        }

        private void Copiar()
        {
            grilla.EndEdit();
            if (_pedido is null || _pedido.EstaVacio)
            {
                MostrarMensaje("No hay nada para pedir.", Estilos.Advertencia);
                return;
            }

            var hoy = DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
            Clipboard.SetText(_pedido.ATexto(NombreNegocio, hoy));
            MostrarMensaje("Pedido copiado. Pegalo en WhatsApp o en un mail (Ctrl+V).", Estilos.Exito);
        }

        private void MostrarMensaje(string texto, Color color)
        {
            lblMensaje.Text = texto;
            lblMensaje.ForeColor = color;
        }
    }
}
