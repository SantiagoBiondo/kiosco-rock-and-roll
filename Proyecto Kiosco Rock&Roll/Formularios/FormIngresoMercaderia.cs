using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>
    /// Para cuando llega un pedido: se escanea cada producto y se suma al stock.
    /// Si el código no existe, se abre el alta con el código y la cantidad ya cargados.
    /// </summary>
    public class FormIngresoMercaderia : Form
    {
        private readonly InventarioServicio _inventario;
        private readonly IVentanas _ventanas;

        private readonly TextBox txtCodigo = new() { Width = 380, Font = Estilos.Grande, PlaceholderText = "Escaneá el producto" };
        private readonly NumericUpDown numCantidad = Estilos.CampoNumero(0, 100_000);
        private readonly ListBox lstIngresos = new() { Dock = DockStyle.Fill, Font = Estilos.Normal, IntegralHeight = false };
        private readonly Label lblEstado = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 36, Padding = new Padding(6), Font = Estilos.Normal };

        public FormIngresoMercaderia(InventarioServicio inventario, IVentanas ventanas)
        {
            _inventario = inventario;
            _ventanas = ventanas;

            Text = "Ingreso de mercadería";
            Font = Estilos.Normal;
            Size = new Size(800, 550);
            StartPosition = FormStartPosition.CenterParent;

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
                Text = "Primero poné la cantidad (si es más de 1) y después escaneá. Esc para cerrar."
            };

            Controls.Add(lstIngresos);
            Controls.Add(lblEstado);
            Controls.Add(ayuda);
            Controls.Add(arriba);

            txtCodigo.KeyDown += TxtCodigo_KeyDown;
            Shown += (_, _) => txtCodigo.Focus();
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
            }
            else if (_ventanas.AltaProducto(this, codigo, cantidad) is { } nuevo)
            {
                Registrar($"NUEVO  {nuevo.Nombre}  (stock: {nuevo.Stock})");
            }
            else
            {
                MostrarEstado("No se cargó el producto nuevo.", Estilos.Advertencia);
            }

            numCantidad.Value = 1;
            txtCodigo.Focus();
        }

        private void Registrar(string texto)
        {
            lstIngresos.Items.Insert(0, $"{DateTime.Now:HH:mm}   {texto}");
            MostrarEstado(texto, Estilos.Exito);
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
