using System.Diagnostics;
using KioscoRockAndRoll.Datos;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Copias de seguridad: dónde se guardan, hacer una ahora y restaurar una anterior.
    /// El backup automático se hace al cerrar el programa (ver <see cref="FormPrincipal"/>).
    /// </summary>
    public class VistaBackups : Vista
    {
        private readonly GestorDeBackups _backups;

        private readonly Label lblCarpetaPrincipal = new() { AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        private readonly Label lblCarpetaExtra = new() { AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        private readonly Button btnQuitarExtra;
        private readonly DataGridView grilla = new();
        private readonly Label lblEstado = new() { AutoSize = true, Margin = new Padding(12, 14, 3, 3), MaximumSize = new Size(900, 0) };

        public VistaBackups(GestorDeBackups backups)
        {
            _backups = backups;

            var explicacion = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8),
                Text = "Cada vez que cerrás el programa se guarda una copia de seguridad del día. " +
                       $"Se conservan las últimas {GestorDeBackups.CantidadAConservar}.\n" +
                       "Para no depender de esta computadora, elegí una carpeta extra: un pendrive o una carpeta de Google Drive / OneDrive."
            };

            var carpetas = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(6) };
            carpetas.Controls.Add(Estilos.Etiqueta("Carpeta principal:"));
            carpetas.Controls.Add(lblCarpetaPrincipal);
            carpetas.Controls.Add(Estilos.Boton("Abrir carpeta", (_, _) => AbrirEnExplorador(_backups.CarpetaPrincipal), 150));
            carpetas.Controls.Add(Estilos.Etiqueta("Carpeta extra:"));
            carpetas.Controls.Add(lblCarpetaExtra);
            var botonesExtra = new FlowLayoutPanel { AutoSize = true };
            botonesExtra.Controls.Add(Estilos.Boton("Elegir carpeta…", (_, _) => ElegirCarpetaExtra(), 150));
            btnQuitarExtra = Estilos.Boton("Quitar", (_, _) => QuitarCarpetaExtra(), 100);
            botonesExtra.Controls.Add(btnQuitarExtra);
            carpetas.Controls.Add(botonesExtra);

            Estilos.ConfigurarGrilla(grilla);
            grilla.ReadOnly = true;
            Estilos.Columna(grilla, nameof(InfoBackup.Nombre), "Backup", llenar: true);
            Estilos.Columna(grilla, nameof(InfoBackup.Fecha), "Última actualización", "dd/MM/yyyy HH:mm", 200);
            Estilos.Columna(grilla, nameof(InfoBackup.Tamanio), "Tamaño", ancho: 110);

            var abajo = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
            var btnAhora = Estilos.Boton("Hacer backup ahora", (_, _) => HacerBackup(), 200);
            btnAhora.BackColor = Estilos.Exito;
            btnAhora.ForeColor = Color.White;
            abajo.Controls.Add(btnAhora);
            abajo.Controls.Add(Estilos.Boton("Restaurar el elegido…", (_, _) => Restaurar(), 210));
            abajo.Controls.Add(lblEstado);

            Controls.Add(grilla);
            Controls.Add(abajo);
            Controls.Add(carpetas);
            Controls.Add(explicacion);
        }

        public override string Titulo => "Backups";

        public override void AlMostrar()
        {
            Cargar();
            grilla.Focus();
        }

        private void Cargar()
        {
            lblCarpetaPrincipal.Text = _backups.CarpetaPrincipal;
            var extra = _backups.CarpetaExtra;
            lblCarpetaExtra.Text = extra ?? "(ninguna)";
            lblCarpetaExtra.ForeColor = extra is null ? SystemColors.GrayText : SystemColors.ControlText;
            btnQuitarExtra.Enabled = extra is not null;
            grilla.DataSource = _backups.Listar().ToList();
        }

        private void HacerBackup()
        {
            var resultado = _backups.HacerBackup();
            Cargar();
            if (resultado.TodoBien)
                MostrarEstado($"Backup hecho en {resultado.Destinos.Count} carpeta(s).", Estilos.Exito);
            else
                MostrarEstado(DescribirErrores(resultado), Color.Red);
        }

        private void ElegirCarpetaExtra()
        {
            using var dialogo = new FolderBrowserDialog
            {
                Description = "Elegí dónde guardar una segunda copia de los backups (pendrive, Google Drive, etc.)",
                UseDescriptionForTitle = true,
                SelectedPath = _backups.CarpetaExtra ?? ""
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

            _backups.CarpetaExtra = dialogo.SelectedPath;
            Cargar();
            // Se hace un backup enseguida para comprobar que la carpeta sirve.
            HacerBackup();
        }

        private void QuitarCarpetaExtra()
        {
            _backups.CarpetaExtra = null;
            Cargar();
            MostrarEstado("Ya no se copian backups a la carpeta extra (los que ya están ahí no se borran).", SystemColors.ControlText);
        }

        private void Restaurar()
        {
            if (grilla.CurrentRow?.DataBoundItem is not InfoBackup backup)
            {
                MostrarEstado("Elegí de la lista el backup a restaurar.", Estilos.Advertencia);
                return;
            }

            var pregunta =
                $"¿Restaurar \"{backup.Nombre}\" (del {backup.Fecha:dd/MM/yyyy HH:mm})?\n\n" +
                "Se van a perder los cambios hechos después de esa fecha (ventas, productos, precios).\n" +
                "Por las dudas, antes se guarda una copia del estado actual.\n\n" +
                "Después el programa se reinicia.";
            if (!Mensajes.Confirmar(this, pregunta))
                return;

            try
            {
                _backups.Restaurar(backup.Ruta);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or Microsoft.Data.Sqlite.SqliteException)
            {
                MostrarEstado($"No se pudo restaurar: {ex.Message}", Color.Red);
                return;
            }

            Mensajes.Informacion(this, "Backup restaurado. El programa se va a reiniciar.");
            Application.Restart();
        }

        internal static string DescribirErrores(ResultadoBackup resultado) =>
            string.Join("\n", resultado.Destinos
                .Where(d => !d.Exito)
                .Select(d => $"No se pudo hacer el backup en \"{d.Carpeta}\": {d.Error}"));

        private static void AbrirEnExplorador(string carpeta)
        {
            Directory.CreateDirectory(carpeta);
            Process.Start(new ProcessStartInfo { FileName = carpeta, UseShellExecute = true });
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }
    }
}
