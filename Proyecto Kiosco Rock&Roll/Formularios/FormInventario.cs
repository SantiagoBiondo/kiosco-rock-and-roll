using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>Listado de productos: buscar, dar de alta, editar y eliminar.</summary>
    public class FormInventario : Form
    {
        private readonly InventarioServicio _inventario;
        private readonly IVentanas _ventanas;

        private readonly TextBox txtBuscar = new() { Width = 350, Font = Estilos.Grande, PlaceholderText = "Buscar por nombre o código" };
        private readonly CheckBox chkStockBajo = new() { Text = "Solo stock bajo", AutoSize = true, Margin = new Padding(12, 10, 3, 3) };
        private readonly DataGridView grilla = new();
        private readonly Label lblResumen = new() { AutoSize = true, Margin = new Padding(12, 14, 3, 3) };

        public FormInventario(InventarioServicio inventario, IVentanas ventanas)
        {
            _inventario = inventario;
            _ventanas = ventanas;

            Text = "Inventario";
            Font = Estilos.Normal;
            Size = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterParent;

            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            arriba.Controls.Add(txtBuscar);
            arriba.Controls.Add(chkStockBajo);
            arriba.Controls.Add(lblResumen);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(Producto.CodigoBarras), "Código", ancho: 160);
            Estilos.Columna(grilla, nameof(Producto.Nombre), "Producto", llenar: true);
            Estilos.Columna(grilla, nameof(Producto.PrecioCosto), "Costo", "C2");
            Estilos.Columna(grilla, nameof(Producto.PrecioVenta), "Venta", "C2");
            Estilos.Columna(grilla, nameof(Producto.Stock), "Stock", "N0", 80);
            Estilos.Columna(grilla, nameof(Producto.StockMinimo), "Mínimo", "N0", 80);

            // Los productos con poco stock se pintan de rojo.
            grilla.RowPrePaint += (_, e) =>
            {
                if (grilla.Rows[e.RowIndex].DataBoundItem is Producto { StockBajo: true })
                    grilla.Rows[e.RowIndex].DefaultCellStyle.BackColor = Estilos.FondoStockBajo;
            };
            grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };

            var abajo = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            abajo.Controls.Add(Estilos.Boton("Nuevo (Insert)", (_, _) => Nuevo()));
            abajo.Controls.Add(Estilos.Boton("Editar (Enter)", (_, _) => Editar()));
            abajo.Controls.Add(Estilos.Boton("Eliminar (Supr)", (_, _) => Eliminar()));
            abajo.Controls.Add(Estilos.Boton("Cerrar (Esc)", (_, _) => Close()));

            Controls.Add(grilla);
            Controls.Add(abajo);
            Controls.Add(arriba);

            txtBuscar.TextChanged += (_, _) => Cargar();
            chkStockBajo.CheckedChanged += (_, _) => Cargar();
            Shown += (_, _) => txtBuscar.Focus();
            Cargar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Insert: Nuevo(); return true;
                case Keys.Enter: Editar(); return true;
                case Keys.Delete when grilla.Focused: Eliminar(); return true;
                case Keys.Escape: Close(); return true;
                case Keys.Down when txtBuscar.Focused && grilla.Rows.Count > 0: grilla.Focus(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private Producto? Seleccionado => grilla.CurrentRow?.DataBoundItem as Producto;

        private void Cargar(long? seleccionarId = null)
        {
            var todos = _inventario.Buscar(txtBuscar.Text);
            var visibles = chkStockBajo.Checked ? todos.Where(p => p.StockBajo).ToList() : todos.ToList();

            grilla.DataSource = visibles;
            lblResumen.Text = $"{visibles.Count} productos  ·  {todos.Count(p => p.StockBajo)} con stock bajo";

            var indice = visibles.FindIndex(p => p.Id == seleccionarId);
            if (indice >= 0)
                grilla.CurrentCell = grilla.Rows[indice].Cells[0];
        }

        private void Nuevo()
        {
            if (_ventanas.AltaProducto(this) is { } creado)
                Cargar(creado.Id);
        }

        private void Editar()
        {
            if (Seleccionado is { } producto && _ventanas.EditarProducto(this, producto) is { } editado)
                Cargar(editado.Id);
        }

        private void Eliminar()
        {
            if (Seleccionado is not { } producto || !Mensajes.Confirmar(this, $"¿Eliminar \"{producto.Nombre}\"?"))
                return;

            try
            {
                _inventario.Eliminar(producto.Id);
                Cargar();
            }
            catch (ReglaDeNegocioException ex)
            {
                Mensajes.Informacion(this, ex.Message);
            }
        }
    }
}
