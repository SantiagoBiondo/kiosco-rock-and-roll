using System.Drawing.Printing;
using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Controles;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Impresion;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Etiquetas de precio para las góndolas: se escanean los productos y se imprimen en hojas A4,
    /// una al lado de la otra, con el nombre y el precio. A la derecha se ve cómo va a quedar la hoja.
    /// </summary>
    public class VistaEtiquetas : Vista
    {
        private readonly InventarioServicio _inventario;
        private readonly AjustesDeEtiquetas _ajustes;
        private readonly ListaDeEtiquetas _lista = new();
        private bool _cargando;

        private readonly PanelLateral panelLateral = new();
        private readonly BuscadorProducto buscador;

        private readonly TextBox txtCodigo = new() { Width = 380, Font = Estilos.Grande, PlaceholderText = "Escaneá el producto" };
        private readonly DataGridView grilla = new();
        private readonly Label lblEstado = new() { AutoSize = true, Margin = new Padding(12, 14, 3, 3), MaximumSize = new Size(600, 0) };
        private readonly Label lblTotal = new() { AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 12, 12, 3) };
        private readonly ComboBox cmbImpresora = Estilos.Opciones<string>([], nombre => nombre, 300);

        private readonly PrintPreviewControl vistaPrevia = new() { Dock = DockStyle.Fill, AutoZoom = true };
        private readonly Label lblHoja = new() { AutoSize = true, Margin = new Padding(8, 12, 8, 3) };
        private readonly Button btnHojaAnterior;
        private readonly Button btnHojaSiguiente;

        public VistaEtiquetas(InventarioServicio inventario, AjustesDeEtiquetas ajustes, BuscadorProducto buscador)
        {
            _inventario = inventario;
            _ajustes = ajustes;
            this.buscador = buscador;

            // Zona de escaneo.
            var escaneo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            escaneo.Controls.Add(new Label { Text = "Código:", AutoSize = true, Font = Estilos.Grande, Margin = new Padding(3, 8, 3, 3) });
            escaneo.Controls.Add(txtCodigo);
            escaneo.Controls.Add(Estilos.Boton("Buscar por nombre (F5)", (_, _) => AbrirBuscador(), 210));
            escaneo.Controls.Add(new Label
            {
                Text = "Tip: \"5*código\" agrega 5 etiquetas",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(12, 12, 3, 3)
            });

            // Lista de productos. Se redibuja desde la ListaDeEtiquetas cada vez que cambia.
            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, "Nombre", "Producto", llenar: true);
            Estilos.Columna(grilla, "Precio", "Precio", "C2", 130);
            Estilos.Columna(grilla, "Cantidad", "Etiquetas", "N0", 100);
            grilla.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete) { QuitarSeleccionado(); e.Handled = true; }
            };

            // Barra de abajo: acciones sobre la lista, impresora e imprimir.
            var acciones = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            acciones.Controls.Add(Estilos.Boton("+1", (_, _) => SumarAlSeleccionado(+1), 60));
            acciones.Controls.Add(Estilos.Boton("-1", (_, _) => SumarAlSeleccionado(-1), 60));
            acciones.Controls.Add(Estilos.Boton("Quitar (Supr)", (_, _) => QuitarSeleccionado(), 150));
            acciones.Controls.Add(Estilos.Boton("Vaciar lista", (_, _) => Vaciar(), 150));
            acciones.Controls.Add(lblEstado);

            var imprimir = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            imprimir.Controls.Add(lblTotal);
            imprimir.Controls.Add(new Label { Text = "Impresora:", AutoSize = true, Margin = new Padding(12, 14, 3, 3) });
            cmbImpresora.Margin = new Padding(3, 10, 3, 3);
            imprimir.Controls.Add(cmbImpresora);
            var btnImprimir = Estilos.Boton("IMPRIMIR (F12)", (_, _) => Imprimir(), 200);
            btnImprimir.Height = 60;
            btnImprimir.Font = Estilos.Grande;
            Estilos.Destacar(btnImprimir, Estilos.FondoBotonPrincipal);
            imprimir.Controls.Add(btnImprimir);

            // Vista previa de la hoja.
            var previa = new Panel { Dock = DockStyle.Right, Width = 560, Padding = new Padding(8) };
            var navegacion = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            navegacion.Controls.Add(Estilos.Etiqueta("Así sale la hoja", Estilos.Titulo));
            btnHojaAnterior = Estilos.Boton("◀", (_, _) => CambiarHoja(-1), 50);
            btnHojaSiguiente = Estilos.Boton("▶", (_, _) => CambiarHoja(+1), 50);
            navegacion.Controls.Add(btnHojaAnterior);
            navegacion.Controls.Add(lblHoja);
            navegacion.Controls.Add(btnHojaSiguiente);
            previa.Controls.Add(vistaPrevia);
            previa.Controls.Add(navegacion);

            // El orden importa: lo que tiene Dock = Fill va primero.
            var principal = new Panel { Dock = DockStyle.Fill };
            principal.Controls.Add(grilla);
            principal.Controls.Add(acciones);
            principal.Controls.Add(imprimir);
            principal.Controls.Add(escaneo);
            Controls.Add(principal);
            Controls.Add(previa);
            Controls.Add(panelLateral);

            txtCodigo.KeyDown += TxtCodigo_KeyDown;
            _lista.Cambio += (_, _) => Dibujar();
            cmbImpresora.SelectedIndexChanged += (_, _) => GuardarImpresora();

            buscador.Elegido += (_, producto) => { CerrarPanel(); Agregar(producto, 1); };
            buscador.Cancelado += (_, _) => CerrarPanel();

            Dibujar();
        }

        public override string Titulo => "Etiquetas";

        public override void AlMostrar()
        {
            CargarImpresoras();
            // Los precios pudieron cambiar en otra sección (por ejemplo, un aumento): se toman los nuevos.
            if (!_lista.EstaVacia)
                _lista.Refrescar(_inventario.ObtenerPorId);
            if (panelLateral.Abierto)
                buscador.Activar();
            else
                txtCodigo.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Las teclas que maneja el buscador le llegan primero a él.
            switch (keyData)
            {
                case Keys.F5: AbrirBuscador(); return true;
                case Keys.F12: Imprimir(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // El buscador puede no estar en pantalla: se libera a mano.
                buscador.Dispose();
                vistaPrevia.Document?.Dispose();
            }
            base.Dispose(disposing);
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
                    MostrarEstado("No se entendió el código. Usá \"cantidad*código\", por ejemplo 5*779123.", Estilos.Error);
                return;
            }

            if (_inventario.ObtenerPorCodigo(escaneado.Codigo) is { } producto)
                Agregar(producto, escaneado.Cantidad);
            else
                MostrarEstado($"El código {escaneado.Codigo} no está registrado. Cargalo primero en Inventario.", Estilos.Error);
        }

        private void Agregar(Producto producto, int cantidad)
        {
            var pedida = _lista.Agregar(producto, cantidad);
            Seleccionar(producto.Id);
            MostrarEstado($"{producto.Nombre}: {Etiquetas(pedida.Cantidad)}", SystemColors.ControlText);
            txtCodigo.Focus();
        }

        // ---------- Lista ----------

        private long? SeleccionadoId => grilla.CurrentRow?.Tag as long?;

        private void SumarAlSeleccionado(int diferencia)
        {
            if (SeleccionadoId is not { } id || _lista.Buscar(id) is not { } pedida)
                return;
            _lista.CambiarCantidad(id, pedida.Cantidad + diferencia);
            Seleccionar(id);
        }

        private void QuitarSeleccionado()
        {
            if (SeleccionadoId is { } id)
                _lista.Quitar(id);
        }

        private void Vaciar()
        {
            if (_lista.EstaVacia || !Mensajes.Confirmar(this, "¿Vaciar la lista de etiquetas?"))
                return;
            _lista.Vaciar();
            MostrarEstado("Lista vacía.", SystemColors.GrayText);
            txtCodigo.Focus();
        }

        private void Seleccionar(long productoId)
        {
            foreach (DataGridViewRow fila in grilla.Rows)
            {
                if (fila.Tag as long? == productoId)
                {
                    grilla.CurrentCell = fila.Cells[0];
                    return;
                }
            }
        }

        private void Dibujar()
        {
            var seleccionado = SeleccionadoId;
            grilla.Rows.Clear();
            foreach (var pedida in _lista.Pedidas)
            {
                var indice = grilla.Rows.Add(pedida.Nombre, pedida.Precio, pedida.Cantidad);
                grilla.Rows[indice].Tag = pedida.Producto.Id;
            }
            if (seleccionado is { } id)
                Seleccionar(id);

            var hojas = _lista.Hojas(ImpresoraDeEtiquetas.PorHoja);
            lblTotal.Text = _lista.EstaVacia
                ? "Sin etiquetas"
                : $"{Etiquetas(_lista.Total)}  ·  {(hojas == 1 ? "1 hoja" : $"{hojas} hojas")}";
            ActualizarVistaPrevia();
        }

        private static string Etiquetas(int cantidad) => cantidad == 1 ? "1 etiqueta" : $"{cantidad} etiquetas";

        // ---------- Vista previa ----------

        private void ActualizarVistaPrevia()
        {
            var anterior = vistaPrevia.Document;
            try
            {
                vistaPrevia.Document = _lista.EstaVacia
                    ? null
                    : ImpresoraDeEtiquetas.Documento(_lista.Etiquetas(), ImpresoraElegida);
            }
            catch (InvalidPrinterException)
            {
                vistaPrevia.Document = null;
                MostrarEstado("No se puede mostrar la hoja: no hay ninguna impresora instalada.", Estilos.Advertencia);
            }
            anterior?.Dispose();

            var hojas = _lista.Hojas(ImpresoraDeEtiquetas.PorHoja);
            vistaPrevia.StartPage = Math.Clamp(vistaPrevia.StartPage, 0, Math.Max(hojas - 1, 0));
            MostrarHoja(hojas);
        }

        private void CambiarHoja(int diferencia)
        {
            var hojas = _lista.Hojas(ImpresoraDeEtiquetas.PorHoja);
            vistaPrevia.StartPage = Math.Clamp(vistaPrevia.StartPage + diferencia, 0, Math.Max(hojas - 1, 0));
            MostrarHoja(hojas);
        }

        private void MostrarHoja(int hojas)
        {
            lblHoja.Text = hojas == 0 ? "" : $"Hoja {vistaPrevia.StartPage + 1} de {hojas}";
            btnHojaAnterior.Visible = btnHojaSiguiente.Visible = hojas > 1;
            btnHojaAnterior.Enabled = vistaPrevia.StartPage > 0;
            btnHojaSiguiente.Enabled = vistaPrevia.StartPage < hojas - 1;
        }

        // ---------- Impresora ----------

        private string? ImpresoraElegida => cmbImpresora.SelectedItem as string;

        /// <summary>Lista las impresoras de Windows y elige la guardada (o la predeterminada).</summary>
        private void CargarImpresoras()
        {
            var instaladas = ImpresoraDeTickets.ImpresorasInstaladas();
            var elegida = _ajustes.Impresora ?? new PrinterSettings().PrinterName;

            _cargando = true;
            Estilos.CambiarOpciones(cmbImpresora, instaladas);
            Estilos.Elegir<string>(cmbImpresora, nombre => nombre == elegida);
            _cargando = false;
            ActualizarVistaPrevia();
        }

        private void GuardarImpresora()
        {
            if (_cargando)
                return;
            _ajustes.Impresora = ImpresoraElegida;
            ActualizarVistaPrevia(); // el tamaño exacto del A4 depende de la impresora
        }

        private void Imprimir()
        {
            if (_lista.EstaVacia)
            {
                MostrarEstado("No hay etiquetas para imprimir: escaneá algún producto.", Estilos.Advertencia);
                txtCodigo.Focus();
                return;
            }

            var hojas = _lista.Hojas(ImpresoraDeEtiquetas.PorHoja);
            var pregunta = $"¿Imprimir {Etiquetas(_lista.Total)} ({(hojas == 1 ? "1 hoja A4" : $"{hojas} hojas A4")}) " +
                           $"en \"{ImpresoraElegida}\"?";
            if (!Mensajes.Confirmar(this, pregunta))
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                ImpresoraDeEtiquetas.Imprimir(_lista.Etiquetas(), ImpresoraElegida);
                MostrarEstado($"Se mandaron {Etiquetas(_lista.Total)} a la impresora. " +
                              "Cuando salgan bien, podés vaciar la lista.", Estilos.Exito);
            }
            catch (NoSePudoImprimirException ex)
            {
                MostrarEstado(ex.Message, Estilos.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ---------- Panel lateral ----------

        private void AbrirBuscador()
        {
            panelLateral.Mostrar(buscador);
            buscador.Activar();
        }

        private void CerrarPanel()
        {
            panelLateral.Ocultar();
            txtCodigo.Focus();
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
