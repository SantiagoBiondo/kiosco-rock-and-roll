using KioscoRockAndRoll.Dominio.Servicios;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Controles;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Para cuando llega un pedido: se escanea cada producto y se suma al stock.
    /// Si el código no existe, se da de alta en el panel de la derecha con el código y la cantidad ya cargados.
    /// </summary>
    public class VistaIngresoMercaderia : Vista
    {
        private readonly InventarioServicio _inventario;

        private readonly PanelLateral panelLateral = new();
        private readonly EditorProducto editor;

        private readonly TextBox txtCodigo = new() { Width = 380, Font = Estilos.Grande, PlaceholderText = "Escaneá el producto" };
        private readonly NumericUpDown numCantidad = Estilos.CampoNumero(0, 100_000);
        private readonly ListBox lstIngresos = new() { Dock = DockStyle.Fill, Font = Estilos.Normal, IntegralHeight = false };
        private readonly Label lblEstado = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 36, Padding = new Padding(6), Font = Estilos.Normal };

        public VistaIngresoMercaderia(InventarioServicio inventario, EditorProducto editor)
        {
            _inventario = inventario;
            this.editor = editor;

            numCantidad.Minimum = 1;
            numCantidad.Value = 1;
            numCantidad.Width = 110;
            numCantidad.Font = Estilos.Grande;

            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            arriba.Controls.Add(new Label { Text = "Cantidad:", AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 8, 3, 3) });
            arriba.Controls.Add(numCantidad);
            arriba.Controls.Add(new Label { Text = "Código:", AutoSize = true, Font = Estilos.Grande, Margin = new Padding(12, 8, 3, 3) });
            arriba.Controls.Add(txtCodigo);

            var ayuda = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 4, 0, 0),
                ForeColor = SystemColors.GrayText,
                Text = "Primero poné la cantidad (si es más de 1) y después escaneá. Abajo queda el registro de lo ingresado."
            };

            var principal = new Panel { Dock = DockStyle.Fill };
            principal.Controls.Add(lstIngresos);
            principal.Controls.Add(lblEstado);
            principal.Controls.Add(ayuda);
            principal.Controls.Add(arriba);
            Controls.Add(principal);
            Controls.Add(panelLateral);

            txtCodigo.KeyDown += TxtCodigo_KeyDown;
            editor.Guardado += (_, nuevo) =>
            {
                CerrarEditor();
                Registrar($"NUEVO  {nuevo.Nombre}  (stock: {nuevo.Stock})");
            };
            editor.Cancelado += (_, _) =>
            {
                CerrarEditor();
                MostrarEstado("No se cargó el producto nuevo.", Estilos.Advertencia);
            };
        }

        public override string Titulo => "Ingreso mercadería";
        public override Keys Atajo => Keys.F3;

        public override void AlMostrar()
        {
            if (panelLateral.Abierto)
                editor.Enfocar();
            else
                txtCodigo.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                editor.Dispose();
            base.Dispose(disposing);
        }

        private void TxtCodigo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;

            var codigo = txtCodigo.Text.Trim();
            txtCodigo.Clear();
            if (codigo == "")
                return;

            var cantidad = (int)numCantidad.Value;
            if (_inventario.IngresarMercaderia(codigo, cantidad) is { } existente)
            {
                Registrar($"+{cantidad}  {existente.Nombre}  (stock: {existente.Stock})");
                return;
            }

            editor.NuevoProducto(codigo, cantidad);
            panelLateral.Mostrar(editor);
            txtCodigo.Enabled = false;
            editor.Enfocar();
            MostrarEstado($"El código {codigo} no está registrado: completá los datos a la derecha.", Estilos.Advertencia);
        }

        private void CerrarEditor()
        {
            panelLateral.Ocultar();
            txtCodigo.Enabled = true;
            txtCodigo.Focus();
        }

        private void Registrar(string texto)
        {
            lstIngresos.Items.Insert(0, $"{DateTime.Now:HH:mm}   {texto}");
            MostrarEstado(texto, Estilos.Exito);
            numCantidad.Value = 1;
            txtCodigo.Focus();
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
