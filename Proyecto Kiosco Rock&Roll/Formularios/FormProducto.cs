using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>Alta o edición de un producto. Las validaciones las hace el dominio.</summary>
    public class FormProducto : Form
    {
        private readonly InventarioServicio _inventario;
        private long? _idEnEdicion;

        private readonly TextBox txtCodigo = new() { Width = 300 };
        private readonly TextBox txtNombre = new() { Width = 300 };
        private readonly NumericUpDown numCosto = Estilos.CampoNumero(2);
        private readonly NumericUpDown numVenta = Estilos.CampoNumero(2);
        private readonly NumericUpDown numStock = Estilos.CampoNumero(0, 1_000_000);
        private readonly NumericUpDown numMinimo = Estilos.CampoNumero(0, 1_000_000);

        public Producto? Resultado { get; private set; }

        public FormProducto(InventarioServicio inventario)
        {
            _inventario = inventario;

            Font = Estilos.Normal;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            AgregarFila(tabla, "Código de barras:", txtCodigo);
            AgregarFila(tabla, "Nombre:", txtNombre);
            AgregarFila(tabla, "Precio de costo:", numCosto);
            AgregarFila(tabla, "Precio de venta:", numVenta);
            AgregarFila(tabla, "Stock actual:", numStock);
            AgregarFila(tabla, "Stock mínimo (aviso):", numMinimo);

            var btnGuardar = Estilos.Boton("Guardar", (_, _) => Guardar(), 130);
            var btnCancelar = Estilos.Boton("Cancelar", (_, _) => Close(), 130);
            btnCancelar.DialogResult = DialogResult.Cancel;
            var botones = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            botones.Controls.AddRange([btnCancelar, btnGuardar]);
            tabla.Controls.Add(botones);
            tabla.SetColumnSpan(botones, 2);

            Controls.Add(tabla);
            AcceptButton = btnGuardar;
            CancelButton = btnCancelar;

            // Si el código ya viene cargado (lo escanearon), arrancamos por el nombre.
            Shown += (_, _) => (txtCodigo.Text == "" ? txtCodigo : txtNombre).Focus();
        }

        public void PrepararAlta(string codigoBarras, int stockInicial)
        {
            Text = "Nuevo producto";
            _idEnEdicion = null;
            txtCodigo.Text = codigoBarras;
            numStock.Value = Math.Clamp(stockInicial, 0, (int)numStock.Maximum);
        }

        public void PrepararEdicion(Producto producto)
        {
            Text = "Editar producto";
            _idEnEdicion = producto.Id;
            txtCodigo.Text = producto.CodigoBarras;
            txtNombre.Text = producto.Nombre;
            numCosto.Value = producto.PrecioCosto;
            numVenta.Value = producto.PrecioVenta;
            numStock.Value = Math.Max(0, producto.Stock);
            numMinimo.Value = producto.StockMinimo;
        }

        private static void AgregarFila(TableLayoutPanel tabla, string etiqueta, Control campo)
        {
            tabla.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) });
            tabla.Controls.Add(campo);
        }

        private void Guardar()
        {
            var datos = new DatosProducto(
                txtCodigo.Text, txtNombre.Text, numCosto.Value, numVenta.Value, (int)numStock.Value, (int)numMinimo.Value);

            if (datos.PrecioVenta < datos.PrecioCosto &&
                !Mensajes.Confirmar(this, "El precio de venta es menor al de costo. ¿Guardar igual?"))
                return;

            try
            {
                Resultado = _idEnEdicion is { } id
                    ? _inventario.Modificar(id, datos)
                    : _inventario.Crear(datos);
            }
            catch (ReglaDeNegocioException ex)
            {
                Mensajes.Advertencia(this, ex.Message);
                return;
            }

            DialogResult = DialogResult.OK;
        }
    }
}
