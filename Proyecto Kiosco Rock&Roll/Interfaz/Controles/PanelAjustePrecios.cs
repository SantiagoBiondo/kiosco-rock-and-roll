using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Aumento (o baja) de precios por porcentaje para un grupo de productos,
    /// con vista previa antes de aplicar. Enter aplica, Esc cancela.
    /// </summary>
    public class PanelAjustePrecios : UserControl
    {
        private static readonly decimal[] OpcionesRedondeo = [0, 1, 5, 10, 50, 100];

        private readonly InventarioServicio _inventario;
        private IReadOnlyList<Producto> _productos = [];

        private readonly NumericUpDown numPorcentaje = Estilos.CampoNumero(2, AjustePrecios.PorcentajeMaximo);
        private readonly ComboBox cmbPrecios = Estilos.Opciones(Enum.GetValues<PreciosAAjustar>(), p => p.Descripcion());
        private readonly ComboBox cmbRedondeo = Estilos.Opciones(OpcionesRedondeo,
            multiplo => multiplo == 0 ? "Sin redondear" : $"Hacia arriba, a múltiplos de {multiplo:C0}");
        private readonly Label lblResumen = new() { AutoSize = true, MaximumSize = new Size(440, 0), Margin = new Padding(3, 10, 3, 6) };
        private readonly DataGridView grilla = new();
        private readonly DataGridViewTextBoxColumn colAntes;
        private readonly DataGridViewTextBoxColumn colDespues;

        /// <summary>Se aplicó el ajuste. El valor es la cantidad de productos actualizados.</summary>
        public event EventHandler<int>? Aplicado;

        public event EventHandler? Cancelado;

        public PanelAjustePrecios(InventarioServicio inventario)
        {
            _inventario = inventario;
            Font = Estilos.Normal;

            numPorcentaje.Minimum = -99.99m;
            numPorcentaje.Width = 140;
            numPorcentaje.Font = Estilos.Grande;

            var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
            var titulo = Estilos.Etiqueta("Actualizar precios", Estilos.Titulo);
            tabla.Controls.Add(titulo);
            tabla.SetColumnSpan(titulo, 2);
            tabla.Controls.Add(Estilos.Etiqueta("Porcentaje (%)"));
            tabla.Controls.Add(numPorcentaje);
            tabla.Controls.Add(Estilos.Etiqueta("Qué precios"));
            tabla.Controls.Add(cmbPrecios);
            tabla.Controls.Add(Estilos.Etiqueta("Redondeo venta"));
            tabla.Controls.Add(cmbRedondeo);
            tabla.Controls.Add(lblResumen);
            tabla.SetColumnSpan(lblResumen, 2);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            grilla.TabStop = false;
            Estilos.Columna(grilla, nameof(CambioDePrecio.Nombre), "Producto", llenar: true);
            colAntes = Estilos.Columna(grilla, nameof(CambioDePrecio.VentaAntes), "Antes", "C2", 105);
            colDespues = Estilos.Columna(grilla, nameof(CambioDePrecio.VentaDespues), "Después", "C2", 105);
            colDespues.DefaultCellStyle.Font = new Font(Estilos.Normal, FontStyle.Bold);

            var botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            var btnAplicar = Estilos.Boton("Aplicar (Enter)", (_, _) => Aplicar(), 160);
            btnAplicar.BackColor = Estilos.FondoBotonPrincipal;
            btnAplicar.ForeColor = Color.White;
            botones.Controls.Add(btnAplicar);
            botones.Controls.Add(Estilos.Boton("Cancelar (Esc)", (_, _) => Cancelado?.Invoke(this, EventArgs.Empty), 160));

            Controls.Add(grilla);
            Controls.Add(botones);
            Controls.Add(tabla);

            numPorcentaje.ValueChanged += (_, _) => ActualizarVistaPrevia();
            numPorcentaje.KeyUp += (_, _) => ActualizarVistaPrevia();
            cmbPrecios.SelectedIndexChanged += (_, _) => ActualizarVistaPrevia();
            cmbRedondeo.SelectedIndexChanged += (_, _) => ActualizarVistaPrevia();
        }

        /// <param name="productos">Los productos a los que se va a aplicar el ajuste.</param>
        public void Iniciar(IReadOnlyList<Producto> productos)
        {
            numPorcentaje.Value = 0;
            numPorcentaje.Text = "";
            CambiarProductos(productos);
            numPorcentaje.Focus();
        }

        /// <summary>Cambia el grupo de productos sin perder lo que ya se cargó (por ejemplo, al filtrar la lista).</summary>
        public void CambiarProductos(IReadOnlyList<Producto> productos)
        {
            _productos = productos;
            ActualizarVistaPrevia();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Aplicar(); return true;
                case Keys.Escape: Cancelado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private PreciosAAjustar PreciosElegidos => (PreciosAAjustar)cmbPrecios.SelectedItem!;

        /// <summary>Arma el ajuste con lo cargado en pantalla. Si no es válido devuelve null y dice por qué.</summary>
        private AjustePrecios? LeerAjuste(out string error)
        {
            error = "";
            try
            {
                return new AjustePrecios(numPorcentaje.ValorTipeado(), PreciosElegidos, (decimal)cmbRedondeo.SelectedItem!);
            }
            catch (ReglaDeNegocioException ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private void ActualizarVistaPrevia()
        {
            // El redondeo solo aplica a la venta.
            cmbRedondeo.Enabled = PreciosElegidos != PreciosAAjustar.SoloCosto;

            // Si solo se toca el costo, la vista previa muestra el costo; si no, la venta.
            var muestraCosto = PreciosElegidos == PreciosAAjustar.SoloCosto;
            colAntes.DataPropertyName = muestraCosto ? nameof(CambioDePrecio.CostoAntes) : nameof(CambioDePrecio.VentaAntes);
            colDespues.DataPropertyName = muestraCosto ? nameof(CambioDePrecio.CostoDespues) : nameof(CambioDePrecio.VentaDespues);
            colAntes.HeaderText = muestraCosto ? "Costo antes" : "Venta antes";
            colDespues.HeaderText = muestraCosto ? "Costo nuevo" : "Venta nueva";

            var cantidad = $"Se aplica a {_productos.Count} productos (los que se ven en la lista).";
            if (LeerAjuste(out var error) is not { } ajuste)
            {
                grilla.DataSource = null;
                lblResumen.Text = $"{cantidad}\n{error}";
                lblResumen.ForeColor = SystemColors.GrayText;
                return;
            }

            grilla.DataSource = _inventario.PrevisualizarAjuste(_productos, ajuste).ToList();
            lblResumen.Text = $"{cantidad}\nRevisá cómo quedan y apretá Enter para aplicar.";
            lblResumen.ForeColor = SystemColors.ControlText;
        }

        private void Aplicar()
        {
            if (LeerAjuste(out var error) is not { } ajuste)
            {
                lblResumen.Text = error;
                lblResumen.ForeColor = Estilos.Error;
                return;
            }

            var signo = ajuste.Porcentaje > 0 ? "+" : "";
            var pregunta = $"¿Aplicar {signo}{ajuste.Porcentaje:0.##}% ({ajuste.Precios.Descripcion().ToLower()}) " +
                           $"a {_productos.Count} productos?\n\nNo se puede deshacer automáticamente.";
            if (!Mensajes.Confirmar(this, pregunta))
                return;

            try
            {
                var actualizados = _inventario.AplicarAjuste(_productos.Select(p => p.Id).ToList(), ajuste);
                Aplicado?.Invoke(this, actualizados);
            }
            catch (ReglaDeNegocioException ex)
            {
                lblResumen.Text = ex.Message;
                lblResumen.ForeColor = Estilos.Error;
            }
        }
    }
}
