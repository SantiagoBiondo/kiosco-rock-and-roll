using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Búsqueda de productos por nombre, para los que no tienen código de barras (o lo tienen roto).
    /// Enter elige, Esc cancela.
    /// </summary>
    public class BuscadorProducto : UserControl
    {
        private readonly InventarioServicio _inventario;
        private readonly TextBox txtBuscar = new() { Dock = DockStyle.Top, Font = Estilos.Grande, PlaceholderText = "Escribí parte del nombre..." };
        private readonly DataGridView grilla = new();

        public event EventHandler<Producto>? Elegido;
        public event EventHandler? Cancelado;

        public BuscadorProducto(InventarioServicio inventario)
        {
            _inventario = inventario;
            Font = Estilos.Normal;

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(Producto.Nombre), "Producto", llenar: true);
            Estilos.Columna(grilla, nameof(Producto.PrecioVenta), "Precio", "C2", 110);
            Estilos.Columna(grilla, nameof(Producto.Stock), "Stock", "N0", 70);

            var titulo = new Label { Text = "Buscar producto", Font = Estilos.Titulo, Dock = DockStyle.Top, Height = 40 };
            var ayuda = new Label
            {
                Text = "↓ para ir a la lista · Enter para agregar · Esc para cerrar",
                Dock = DockStyle.Bottom,
                Height = 30,
                ForeColor = SystemColors.GrayText
            };

            Controls.Add(grilla);
            Controls.Add(txtBuscar);
            Controls.Add(titulo);
            Controls.Add(ayuda);

            txtBuscar.TextChanged += (_, _) => Cargar();
            grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Elegir(); };
        }

        public void Activar()
        {
            txtBuscar.Clear();
            Cargar();
            txtBuscar.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Elegir(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
                case Keys.Down when txtBuscar.Focused && grilla.Rows.Count > 0: grilla.Focus(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Cargar() => grilla.DataSource = _inventario.Buscar(txtBuscar.Text);

        private void Elegir()
        {
            if (grilla.CurrentRow?.DataBoundItem is Producto producto)
                Elegido?.Invoke(this, producto);
        }
    }
}
