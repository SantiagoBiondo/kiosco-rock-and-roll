using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Alta o edición de un producto, embebible en cualquier pantalla.
    /// Enter guarda, Esc cancela. Las validaciones las hace el dominio.
    /// </summary>
    public class EditorProducto : UserControl
    {
        private readonly InventarioServicio _inventario;
        private readonly CatalogoServicio<Categoria> _categorias;
        private readonly CatalogoServicio<Proveedor> _proveedores;
        private long? _idEnEdicion;

        private readonly Label lblTitulo = Estilos.Etiqueta("", Estilos.Titulo);
        private readonly TextBox txtCodigo = new() { Width = 220 };
        private readonly TextBox txtNombre = new() { Width = 220 };
        private readonly NumericUpDown numCosto = Estilos.CampoNumero(2);
        private readonly NumericUpDown numVenta = Estilos.CampoNumero(2);
        private readonly NumericUpDown numStock = Estilos.CampoNumero(0, 1_000_000);
        private readonly NumericUpDown numMinimo = Estilos.CampoNumero(0, 1_000_000);
        private readonly ComboBox cmbCategoria = Estilos.Opciones<Opcion<long?>>([], o => o.Texto, 220);
        private readonly ComboBox cmbProveedor = Estilos.Opciones<Opcion<long?>>([], o => o.Texto, 220);
        private readonly Label lblMargen = Estilos.Etiqueta("");
        private readonly Label lblError = new() { AutoSize = true, ForeColor = Estilos.Error, MaximumSize = new Size(400, 0) };

        /// <summary>Se guardó el producto (nuevo o editado).</summary>
        public event EventHandler<Producto>? Guardado;

        public event EventHandler? Cancelado;

        public EditorProducto(InventarioServicio inventario,
            CatalogoServicio<Categoria> categorias, CatalogoServicio<Proveedor> proveedores)
        {
            _inventario = inventario;
            _categorias = categorias;
            _proveedores = proveedores;
            Font = Estilos.Normal;

            var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
            tabla.Controls.Add(lblTitulo);
            tabla.SetColumnSpan(lblTitulo, 2);
            AgregarFila(tabla, "Código de barras", txtCodigo);
            AgregarFila(tabla, "Nombre", txtNombre);
            AgregarFila(tabla, "Precio de costo", numCosto);
            AgregarFila(tabla, "Precio de venta", numVenta);
            AgregarFila(tabla, "", lblMargen);
            AgregarFila(tabla, "Stock actual", numStock);
            AgregarFila(tabla, "Stock mínimo", numMinimo);
            AgregarFila(tabla, "Categoría", cmbCategoria);
            AgregarFila(tabla, "Proveedor", cmbProveedor);

            var botones = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            botones.Controls.Add(Estilos.Boton("Guardar (Enter)", (_, _) => Guardar(), 160));
            botones.Controls.Add(Estilos.Boton("Cancelar (Esc)", (_, _) => Cancelado?.Invoke(this, EventArgs.Empty), 160));
            tabla.Controls.Add(botones);
            tabla.SetColumnSpan(botones, 2);
            tabla.Controls.Add(lblError);
            tabla.SetColumnSpan(lblError, 2);

            Controls.Add(tabla);

            foreach (var campo in new[] { numCosto, numVenta })
            {
                campo.ValueChanged += (_, _) => ActualizarMargen();
                campo.KeyUp += (_, _) => ActualizarMargen();
            }
        }

        public void NuevoProducto(string codigoBarras = "", int stockInicial = 0)
        {
            _idEnEdicion = null;
            lblTitulo.Text = "Nuevo producto";
            Cargar("", "", 0, 0, stockInicial, 0, null, null);
            txtCodigo.Text = codigoBarras;
        }

        public void Editar(Producto producto)
        {
            _idEnEdicion = producto.Id;
            lblTitulo.Text = "Editar producto";
            Cargar(producto.CodigoBarras, producto.Nombre, producto.PrecioCosto, producto.PrecioVenta,
                Math.Max(0, producto.Stock), producto.StockMinimo, producto.CategoriaId, producto.ProveedorId);
        }

        /// <summary>Pone el cursor en el primer campo que falta completar.</summary>
        public void Enfocar()
        {
            var campo = txtCodigo.Text == "" ? txtCodigo : txtNombre;
            campo.Focus();
            campo.SelectAll();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Guardar(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Cargar(string codigo, string nombre, decimal costo, decimal venta, int stock, int minimo,
            long? categoriaId, long? proveedorId)
        {
            txtCodigo.Text = codigo;
            txtNombre.Text = nombre;
            numCosto.Value = costo;
            numVenta.Value = venta;
            numStock.Value = Math.Min(stock, (int)numStock.Maximum);
            numMinimo.Value = minimo;

            // Las listas se recargan cada vez, por si se agregaron categorías o proveedores.
            Estilos.CambiarOpciones(cmbCategoria, OpcionesDeCatalogo.ParaAsignar(_categorias.Listar(), "(sin categoría)"));
            Estilos.CambiarOpciones(cmbProveedor, OpcionesDeCatalogo.ParaAsignar(_proveedores.Listar(), "(sin proveedor)"));
            Estilos.Elegir<Opcion<long?>>(cmbCategoria, o => o.Valor == categoriaId);
            Estilos.Elegir<Opcion<long?>>(cmbProveedor, o => o.Valor == proveedorId);

            lblError.Text = "";
            ActualizarMargen();
        }

        private static void AgregarFila(TableLayoutPanel tabla, string etiqueta, Control campo)
        {
            tabla.Controls.Add(Estilos.Etiqueta(etiqueta));
            tabla.Controls.Add(campo);
        }

        private void ActualizarMargen()
        {
            var costo = numCosto.ValorTipeado();
            var venta = numVenta.ValorTipeado();
            if (costo <= 0 || venta <= 0)
            {
                lblMargen.Text = "";
                return;
            }

            var margen = (venta - costo) / costo;
            lblMargen.Text = margen < 0 ? $"¡Vende a pérdida! ({margen:P0})" : $"Ganancia: {margen:P0}";
            lblMargen.ForeColor = margen < 0 ? Estilos.Error : Estilos.Exito;
        }

        private void Guardar()
        {
            var datos = new DatosProducto(
                txtCodigo.Text, txtNombre.Text, numCosto.Value, numVenta.Value, (int)numStock.Value, (int)numMinimo.Value,
                ((Opcion<long?>)cmbCategoria.SelectedItem!).Valor, ((Opcion<long?>)cmbProveedor.SelectedItem!).Valor);

            try
            {
                var producto = _idEnEdicion is { } id
                    ? _inventario.Modificar(id, datos)
                    : _inventario.Crear(datos);
                lblError.Text = "";
                Guardado?.Invoke(this, producto);
            }
            catch (ReglaDeNegocioException ex)
            {
                lblError.Text = ex.Message;
            }
        }
    }
}
