using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>Para vender productos sin código de barras (o con el código roto): se buscan por nombre.</summary>
    public class FormBuscarProducto : Form
    {
        private readonly InventarioServicio _inventario;
        private readonly TextBox txtBuscar = new() { Dock = DockStyle.Top, Font = Estilos.Grande, PlaceholderText = "Escribí parte del nombre..." };
        private readonly DataGridView grilla = new();

        public Producto? Seleccionado { get; private set; }

        public FormBuscarProducto(InventarioServicio inventario)
        {
            _inventario = inventario;

            Text = "Buscar producto";
            Font = Estilos.Normal;
            Size = new Size(750, 500);
            StartPosition = FormStartPosition.CenterParent;

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(Producto.CodigoBarras), "Código", ancho: 150);
            Estilos.Columna(grilla, nameof(Producto.Nombre), "Producto", llenar: true);
            Estilos.Columna(grilla, nameof(Producto.PrecioVenta), "Precio", "C2");
            Estilos.Columna(grilla, nameof(Producto.Stock), "Stock", "N0", 80);

            Controls.Add(grilla);
            Controls.Add(txtBuscar);

            txtBuscar.TextChanged += (_, _) => Cargar();
            grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Elegir(); };
            Cargar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Elegir(); return true;
                case Keys.Escape: Close(); return true;
                case Keys.Down when txtBuscar.Focused && grilla.Rows.Count > 0: grilla.Focus(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Cargar() => grilla.DataSource = _inventario.Buscar(txtBuscar.Text);

        private void Elegir()
        {
            if (grilla.CurrentRow?.DataBoundItem is not Producto producto)
                return;
            Seleccionado = producto;
            DialogResult = DialogResult.OK;
        }
    }
}
