using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>
    /// Pantalla principal: se escanean los productos y se cobra.
    /// El lector de códigos funciona como un teclado: escribe el código y aprieta Enter.
    /// La lógica del carrito vive en <see cref="Carrito"/>; este formulario solo la muestra.
    /// </summary>
    public class FormVenta : Form
    {
        private readonly InventarioServicio _inventario;
        private readonly VentaServicio _ventas;
        private readonly IVentanas _ventanas;
        private readonly Carrito _carrito = new();

        private readonly TextBox txtCodigo = new() { Font = Estilos.Grande, Width = 420, PlaceholderText = "Escaneá o escribí el código" };
        private readonly DataGridView grilla = new();
        private readonly DataGridViewTextBoxColumn colCantidad;
        private readonly Label lblTotal = new() { Font = Estilos.Total, AutoSize = true };
        private readonly Label lblEstado = new() { AutoSize = true, Font = Estilos.Normal, Anchor = AnchorStyles.Left };

        public FormVenta(InventarioServicio inventario, VentaServicio ventas, IVentanas ventanas)
        {
            _inventario = inventario;
            _ventas = ventas;
            _ventanas = ventanas;

            Text = "Kiosco Rock & Roll - Venta";
            Font = Estilos.Normal;
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 550);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            // Barra de arriba: accesos a las otras pantallas.
            var menu = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            menu.Controls.Add(Estilos.Boton("Inventario (F2)", (_, _) => AbrirInventario()));
            menu.Controls.Add(Estilos.Boton("Ingreso mercadería (F3)", (_, _) => AbrirIngreso(), 210));
            menu.Controls.Add(Estilos.Boton("Ventas del día (F4)", (_, _) => AbrirVentasDelDia(), 190));

            // Zona de escaneo.
            var escaneo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            escaneo.Controls.Add(new Label { Text = "Código:", AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 8, 3, 3) });
            escaneo.Controls.Add(txtCodigo);
            escaneo.Controls.Add(Estilos.Boton("Buscar por nombre (F5)", (_, _) => BuscarPorNombre(), 210));
            escaneo.Controls.Add(new Label
            {
                Text = "Tip: \"3*código\" agrega 3 unidades",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(12, 12, 3, 3)
            });

            // Carrito. La grilla no está enlazada a datos: se redibuja desde el Carrito cada vez que cambia.
            Estilos.ConfigurarGrilla(grilla);
            Estilos.Columna(grilla, "Codigo", "Código", ancho: 160);
            Estilos.Columna(grilla, "Nombre", "Producto", llenar: true);
            colCantidad = Estilos.Columna(grilla, "Cantidad", "Cant.", "N0", 90, soloLectura: false);
            Estilos.Columna(grilla, "Precio", "Precio", "C2", 140);
            Estilos.Columna(grilla, "Subtotal", "Subtotal", "C2", 160);
            grilla.CellValidating += Grilla_CellValidating;
            grilla.CellEndEdit += Grilla_CellEndEdit;
            grilla.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete) { QuitarSeleccionado(); e.Handled = true; }
            };

            // Barra de abajo: acciones sobre el carrito, total y cobrar.
            var abajo = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 3, AutoSize = true, Padding = new Padding(6) };
            abajo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            abajo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            abajo.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var acciones = new FlowLayoutPanel { AutoSize = true };
            acciones.Controls.Add(Estilos.Boton("+1", (_, _) => SumarAlSeleccionado(+1), 60));
            acciones.Controls.Add(Estilos.Boton("-1", (_, _) => SumarAlSeleccionado(-1), 60));
            acciones.Controls.Add(Estilos.Boton("Quitar (Supr)", (_, _) => QuitarSeleccionado(), 150));
            acciones.Controls.Add(Estilos.Boton("Cancelar venta (Esc)", (_, _) => CancelarVenta(), 200));

            var totalYCobro = new FlowLayoutPanel { AutoSize = true };
            totalYCobro.Controls.Add(new Label { Text = "TOTAL:", AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 16, 3, 3) });
            totalYCobro.Controls.Add(lblTotal);
            var btnCobrar = Estilos.Boton("COBRAR (F12)", (_, _) => Cobrar(), 200);
            btnCobrar.Height = 60;
            btnCobrar.Font = Estilos.Grande;
            btnCobrar.BackColor = Estilos.Exito;
            btnCobrar.ForeColor = Color.White;
            totalYCobro.Controls.Add(btnCobrar);

            abajo.Controls.Add(acciones, 0, 0);
            abajo.Controls.Add(lblEstado, 1, 0);
            abajo.Controls.Add(totalYCobro, 2, 0);

            // El orden importa: lo que tiene Dock = Fill va primero.
            Controls.Add(grilla);
            Controls.Add(abajo);
            Controls.Add(escaneo);
            Controls.Add(menu);

            txtCodigo.KeyDown += TxtCodigo_KeyDown;
            _carrito.Cambio += (_, _) => DibujarCarrito();
            Shown += (_, _) => txtCodigo.Focus();
            DibujarCarrito();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F2: AbrirInventario(); return true;
                case Keys.F3: AbrirIngreso(); return true;
                case Keys.F4: AbrirVentasDelDia(); return true;
                case Keys.F5: BuscarPorNombre(); return true;
                case Keys.F12: Cobrar(); return true;
                case Keys.Escape when !grilla.IsCurrentCellInEditMode: CancelarVenta(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- Escaneo ----------

        private void TxtCodigo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true; // evita el "beep" de Windows

            var texto = txtCodigo.Text;
            txtCodigo.Clear();

            if (!CodigoEscaneado.TryInterpretar(texto, out var escaneado))
            {
                if (texto.Trim() != "")
                    MostrarEstado("No se entendió el código. Usá \"cantidad*código\", por ejemplo 3*779123.", Color.Red);
                return;
            }

            var producto = _inventario.ObtenerPorCodigo(escaneado.Codigo) ?? OfrecerAlta(escaneado.Codigo);
            if (producto != null)
                Agregar(producto, escaneado.Cantidad);
        }

        private Producto? OfrecerAlta(string codigo)
        {
            if (!Mensajes.Confirmar(this, $"El código {codigo} no está registrado.\n¿Querés darlo de alta ahora?"))
                return null;
            return _ventanas.AltaProducto(this, codigo);
        }

        private void Agregar(Producto producto, int cantidad)
        {
            var item = _carrito.Agregar(producto, cantidad);
            SeleccionarProducto(producto.Id);
            InformarItem(item);
        }

        // ---------- Edición del carrito ----------

        private long? ProductoSeleccionadoId => grilla.CurrentRow?.Tag as long?;

        private void SumarAlSeleccionado(int diferencia)
        {
            if (ProductoSeleccionadoId is not { } id || _carrito.Buscar(id) is not { } item)
                return;

            _carrito.CambiarCantidad(id, item.Cantidad + diferencia);
            if (_carrito.Buscar(id) is { } actualizado)
            {
                SeleccionarProducto(id);
                InformarItem(actualizado);
            }
            txtCodigo.Focus();
        }

        private void QuitarSeleccionado()
        {
            if (ProductoSeleccionadoId is { } id)
                _carrito.Quitar(id);
            txtCodigo.Focus();
        }

        private void Grilla_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != colCantidad.Index || !grilla.IsCurrentCellInEditMode)
                return;
            if (!int.TryParse(e.FormattedValue?.ToString(), out var cantidad) || cantidad < 0)
            {
                MostrarEstado("La cantidad tiene que ser un número entero.", Color.Red);
                e.Cancel = true;
            }
        }

        private void Grilla_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            var fila = grilla.Rows[e.RowIndex];
            if (fila.Tag is not long id || !int.TryParse(fila.Cells[e.ColumnIndex].Value?.ToString(), out var cantidad))
                return;

            // Se difiere porque redibujar la grilla dentro de su propio evento de edición no está permitido.
            BeginInvoke(() =>
            {
                _carrito.CambiarCantidad(id, cantidad);
                txtCodigo.Focus();
            });
        }

        private void CancelarVenta()
        {
            if (!_carrito.EstaVacio && Mensajes.Confirmar(this, "¿Cancelar la venta actual?"))
            {
                _carrito.Vaciar();
                MostrarEstado("Venta cancelada.", SystemColors.GrayText);
            }
            txtCodigo.Focus();
        }

        // ---------- Cobro ----------

        private void Cobrar()
        {
            if (_carrito.EstaVacio)
            {
                MostrarEstado("No hay productos para cobrar.", Estilos.Advertencia);
                txtCodigo.Focus();
                return;
            }

            var cobro = _ventanas.PedirCobro(this, _carrito.Total);
            if (cobro != null)
            {
                var venta = _ventas.Cobrar(_carrito, cobro);
                var vuelto = cobro.Vuelto > 0 ? $"  -  VUELTO: {cobro.Vuelto:C2}" : "";
                MostrarEstado($"Venta Nº {venta.Id} registrada ({venta.Total:C2}, {cobro.Medio.Descripcion()}){vuelto}", Estilos.Exito);
            }
            txtCodigo.Focus();
        }

        // ---------- Otras pantallas ----------

        private void BuscarPorNombre()
        {
            if (_ventanas.BuscarProducto(this) is { } producto)
                Agregar(producto, 1);
            txtCodigo.Focus();
        }

        private void AbrirInventario()
        {
            _ventanas.MostrarInventario(this);
            RefrescarCarrito();
        }

        private void AbrirIngreso()
        {
            _ventanas.MostrarIngresoMercaderia(this);
            RefrescarCarrito();
        }

        private void AbrirVentasDelDia()
        {
            _ventanas.MostrarVentasDelDia(this);
            txtCodigo.Focus();
        }

        // Si se editó un producto (precio, stock) mientras había una venta a medias, el carrito toma los datos nuevos.
        private void RefrescarCarrito()
        {
            if (!_carrito.EstaVacio)
                _carrito.Refrescar(_inventario.ObtenerPorId);
            txtCodigo.Focus();
        }

        // ---------- Presentación ----------

        private void DibujarCarrito()
        {
            var seleccionado = ProductoSeleccionadoId;

            grilla.Rows.Clear();
            foreach (var item in _carrito.Items)
            {
                var indice = grilla.Rows.Add(item.Producto.CodigoBarras, item.Producto.Nombre, item.Cantidad,
                    item.PrecioUnitario, item.Subtotal);
                var fila = grilla.Rows[indice];
                fila.Tag = item.Producto.Id;
                if (item.SuperaElStock)
                    fila.DefaultCellStyle.ForeColor = Estilos.Advertencia;
            }

            lblTotal.Text = _carrito.Total.ToString("C2");
            if (seleccionado is { } id)
                SeleccionarProducto(id);
        }

        private void SeleccionarProducto(long productoId)
        {
            foreach (DataGridViewRow fila in grilla.Rows)
            {
                if (fila.Tag is long id && id == productoId)
                {
                    grilla.CurrentCell = fila.Cells[0];
                    return;
                }
            }
        }

        private void InformarItem(ItemCarrito item)
        {
            if (item.SuperaElStock)
                MostrarEstado($"Ojo: de \"{item.Producto.Nombre}\" figuran solo {item.Producto.Stock} en stock.", Estilos.Advertencia);
            else
                MostrarEstado($"{item.Producto.Nombre}  x{item.Cantidad}", SystemColors.ControlText);
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
