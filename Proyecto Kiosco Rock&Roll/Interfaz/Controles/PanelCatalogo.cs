using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Controles
{
    /// <summary>
    /// Alta, modificación y baja de los elementos de un catálogo (categorías, proveedores).
    /// Arriba la lista; abajo los datos del elegido. Las subclases agregan sus campos propios.
    /// Enter guarda, Insert empieza uno nuevo, Supr elimina (con la lista enfocada), Esc cierra.
    /// </summary>
    public abstract class PanelCatalogo<T> : UserControl where T : ElementoDeCatalogo
    {
        private readonly CatalogoServicio<T> _servicio;
        private T? _enEdicion;

        private readonly Label lblTitulo = Estilos.Etiqueta("", Estilos.Titulo);
        private readonly Label lblEdicion = new() { AutoSize = true, Font = new Font(Estilos.Normal, FontStyle.Bold), Margin = new Padding(3, 12, 3, 3) };
        private readonly TextBox txtNombre = new() { Width = 260 };
        private readonly Label lblMensaje = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
        private readonly TableLayoutPanel campos = new() { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Bottom };

        protected readonly DataGridView grilla = new();

        /// <summary>Se agregó, modificó o eliminó algo: quien lo usa refresca sus listas.</summary>
        public event EventHandler? Cambiado;

        public event EventHandler? Cerrado;

        protected PanelCatalogo(CatalogoServicio<T> servicio)
        {
            _servicio = servicio;
            Font = Estilos.Normal;
        }

        /// <summary>Por ejemplo "Categorías".</summary>
        protected abstract string Titulo { get; }

        /// <summary>Por ejemplo "categoría", para armar los mensajes.</summary>
        protected abstract string Singular { get; }

        /// <summary>Por ejemplo "Nueva categoría" (cada subclase con su género).</summary>
        protected abstract string TextoNuevo { get; }

        /// <summary>Crea un elemento nuevo con lo cargado en pantalla.</summary>
        protected abstract T Crear(string nombre);

        /// <summary>Muestra en los campos propios los datos del elemento (o los vacía si es null).</summary>
        protected virtual void MostrarCamposPropios(T? elemento) { }

        /// <summary>Pasa al elemento lo cargado en los campos propios.</summary>
        protected virtual void AplicarCamposPropios(T elemento) { }

        /// <summary>Para que las subclases agreguen campos y columnas. Se llama una sola vez.</summary>
        protected virtual void ConstruirExtras() { }

        protected void AgregarCampo(string etiqueta, Control campo)
        {
            campos.Controls.Add(Estilos.Etiqueta(etiqueta));
            campos.Controls.Add(campo);
        }

        private bool _construido;

        /// <summary>Muestra la lista actualizada y prepara para cargar uno nuevo.</summary>
        public void Activar()
        {
            if (!_construido)
            {
                Construir();
                _construido = true;
            }
            Cargar();
            EmpezarNuevo();
            MostrarMensaje("", SystemColors.ControlText);
        }

        // Se arma en el primer Activar y no en el constructor, porque usa miembros abstractos
        // que las subclases todavía no inicializaron mientras corre el constructor base.
        private void Construir()
        {
            lblTitulo.Text = Titulo;

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(ElementoDeCatalogo.Nombre), "Nombre", llenar: true);

            AgregarCampo("Nombre", txtNombre);
            ConstruirExtras();

            var botones = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            botones.Controls.Add(Estilos.Boton("Guardar (Enter)", (_, _) => Guardar(), 150));
            botones.Controls.Add(Estilos.Boton("Nuevo (Insert)", (_, _) => EmpezarNuevo(), 140));
            botones.Controls.Add(Estilos.Boton("Eliminar (Supr)", (_, _) => Eliminar(), 140));
            botones.Controls.Add(Estilos.Boton("Cerrar (Esc)", (_, _) => Cerrado?.Invoke(this, EventArgs.Empty), 140));
            campos.Controls.Add(botones);
            campos.SetColumnSpan(botones, 2);
            campos.Controls.Add(lblMensaje);
            campos.SetColumnSpan(lblMensaje, 2);

            var encabezado = new Panel { Dock = DockStyle.Top, Height = 44 };
            encabezado.Controls.Add(lblTitulo);
            var pieLista = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            pieLista.Controls.Add(lblEdicion);

            Controls.Add(grilla);
            Controls.Add(pieLista);
            Controls.Add(campos);
            Controls.Add(encabezado);

            grilla.SelectionChanged += (_, _) =>
            {
                if (grilla.Focused && grilla.CurrentRow?.DataBoundItem is T elegido)
                    EmpezarEdicion(elegido);
            };
            grilla.CellClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && grilla.Rows[e.RowIndex].DataBoundItem is T elegido)
                    EmpezarEdicion(elegido);
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Guardar(); return true;
                case Keys.Insert: EmpezarNuevo(); return true;
                case Keys.Delete when grilla.Focused: Eliminar(); return true;
                case Keys.Escape: Cerrado?.Invoke(this, EventArgs.Empty); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Cargar(long? seleccionarId = null)
        {
            var lista = _servicio.Listar().ToList();
            grilla.DataSource = lista;
            grilla.ClearSelection();

            var indice = lista.FindIndex(e => e.Id == seleccionarId);
            if (indice >= 0)
                grilla.Rows[indice].Selected = true;
        }

        private void EmpezarNuevo()
        {
            _enEdicion = null;
            lblEdicion.Text = $"{TextoNuevo}:";
            txtNombre.Text = "";
            MostrarCamposPropios(null);
            grilla.ClearSelection();
            txtNombre.Focus();
        }

        private void EmpezarEdicion(T elemento)
        {
            _enEdicion = elemento;
            lblEdicion.Text = $"Editando: {elemento.Nombre}";
            txtNombre.Text = elemento.Nombre;
            MostrarCamposPropios(elemento);
        }

        private void Guardar()
        {
            try
            {
                T elemento;
                if (_enEdicion is null)
                {
                    elemento = Crear(txtNombre.Text);
                }
                else
                {
                    // Se trabaja sobre una copia leída de nuevo: si la validación falla,
                    // el elemento de la lista no queda modificado a medias.
                    elemento = _servicio.ObtenerPorId(_enEdicion.Id)
                               ?? throw new ReglaDeNegocioException($"\"{_enEdicion.Nombre}\" ya no existe.");
                    elemento.Renombrar(txtNombre.Text);
                }
                AplicarCamposPropios(elemento);
                _servicio.Guardar(elemento);

                Cargar(elemento.Id);
                EmpezarNuevo();
                MostrarMensaje($"Se guardó \"{elemento.Nombre}\".", Estilos.Exito);
                Cambiado?.Invoke(this, EventArgs.Empty);
            }
            catch (ReglaDeNegocioException ex)
            {
                MostrarMensaje(ex.Message, Color.Red);
            }
        }

        private void Eliminar()
        {
            if (_enEdicion is not { } elemento)
            {
                MostrarMensaje("Elegí de la lista lo que querés eliminar.", Estilos.Advertencia);
                return;
            }

            var productos = _servicio.ContarProductos(elemento.Id);
            var aviso = productos > 0 ? $"\n\n{productos} productos van a quedar sin {Singular} (no se borran)." : "";
            if (!Mensajes.Confirmar(this, $"¿Eliminar \"{elemento.Nombre}\"?{aviso}"))
                return;

            _servicio.Eliminar(elemento.Id);
            Cargar();
            EmpezarNuevo();
            MostrarMensaje($"Se eliminó \"{elemento.Nombre}\".", Estilos.Exito);
            Cambiado?.Invoke(this, EventArgs.Empty);
        }

        private void MostrarMensaje(string texto, Color color)
        {
            lblMensaje.Text = texto;
            lblMensaje.ForeColor = color;
        }
    }

    public sealed class PanelCategorias(CatalogoServicio<Categoria> servicio) : PanelCatalogo<Categoria>(servicio)
    {
        protected override string Titulo => "Categorías";
        protected override string Singular => "categoría";
        protected override string TextoNuevo => "Nueva categoría";
        protected override Categoria Crear(string nombre) => new(nombre);
    }

    public sealed class PanelProveedores(CatalogoServicio<Proveedor> servicio) : PanelCatalogo<Proveedor>(servicio)
    {
        private readonly TextBox txtTelefono = new() { Width = 260, PlaceholderText = "Para mandarle el pedido" };

        protected override string Titulo => "Proveedores";
        protected override string Singular => "proveedor";
        protected override string TextoNuevo => "Nuevo proveedor";

        protected override Proveedor Crear(string nombre) => new(nombre);

        protected override void ConstruirExtras()
        {
            AgregarCampo("Teléfono", txtTelefono);
            Estilos.Columna(grilla, nameof(Proveedor.Telefono), "Teléfono", ancho: 150);
        }

        protected override void MostrarCamposPropios(Proveedor? proveedor) => txtTelefono.Text = proveedor?.Telefono ?? "";

        protected override void AplicarCamposPropios(Proveedor proveedor) => proveedor.CambiarTelefono(txtTelefono.Text);
    }
}
