using System.Diagnostics;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using KioscoRockAndRoll.Reportes;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Ventas y ganancia de un período, agrupadas por día o por mes, y el ranking de productos.
    /// </summary>
    public class VistaReportes : Vista
    {
        /// <summary>Un período rápido de la lista; Calcular es null para "Personalizado".</summary>
        private sealed record OpcionPeriodo(string Texto, Func<DateOnly, PeriodoReporte>? Calcular);

        private static readonly OpcionPeriodo[] Periodos =
        [
            new("Hoy", PeriodoReporte.Dia),
            new("Ayer", hoy => PeriodoReporte.Dia(hoy.AddDays(-1))),
            new("Esta semana", PeriodoReporte.SemanaHasta),
            new("Este mes", PeriodoReporte.MesHasta),
            new("Mes anterior", PeriodoReporte.MesAnterior),
            new("Este año", PeriodoReporte.AnioHasta),
            new("Personalizado", null)
        ];

        private readonly ReporteDeVentasServicio _reportes;
        private readonly ExportadorDeReportes _exportador;
        private readonly TimeProvider _reloj;
        private ReporteDeVentas? _reporte;
        private bool _actualizandoFechas;

        private readonly ComboBox cmbPeriodo = Estilos.Opciones(Periodos, p => p.Texto, 170);
        private readonly DateTimePicker dtpDesde = new() { Format = DateTimePickerFormat.Short, Width = 140 };
        private readonly DateTimePicker dtpHasta = new() { Format = DateTimePickerFormat.Short, Width = 140 };
        private readonly ComboBox cmbAgrupar = Estilos.Opciones(Enum.GetValues<Agrupacion>(),
            a => a == Agrupacion.PorMes ? "Por mes" : "Por día", 130);
        private readonly ComboBox cmbOrden = Estilos.Opciones(Enum.GetValues<OrdenProductos>(), a => a switch
        {
            OrdenProductos.PorUnidades => "Unidades",
            OrdenProductos.PorGanancia => "Ganancia",
            _ => "Recaudación"
        }, 150);

        private readonly Label lblVendido = ValorResumen();
        private readonly Label lblCosto = ValorResumen();
        private readonly Label lblGanancia = ValorResumen();
        private readonly Label lblPorcentaje = ValorResumen();
        private readonly Label lblVentas = ValorResumen();
        private readonly Label lblEstado = new() { AutoSize = true, Margin = new Padding(12, 14, 3, 3), MaximumSize = new Size(600, 0) };
        private readonly DataGridView grillaPeriodos = new();
        private readonly DataGridView grillaProductos = new();

        public VistaReportes(ReporteDeVentasServicio reportes, ExportadorDeReportes exportador, TimeProvider reloj)
        {
            _reportes = reportes;
            _exportador = exportador;
            _reloj = reloj;

            // Filtros.
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            filtros.Controls.Add(Etiqueta("Período:"));
            filtros.Controls.Add(cmbPeriodo);
            filtros.Controls.Add(Etiqueta("Desde:"));
            filtros.Controls.Add(dtpDesde);
            filtros.Controls.Add(Etiqueta("Hasta:"));
            filtros.Controls.Add(dtpHasta);
            filtros.Controls.Add(Etiqueta("Agrupar:"));
            filtros.Controls.Add(cmbAgrupar);
            filtros.Controls.Add(Estilos.Boton("Exportar PDF…", (_, _) => ExportarPdf(), 160));
            filtros.Controls.Add(lblEstado);
            foreach (Control control in filtros.Controls)
                if (control is ComboBox or DateTimePicker)
                    control.Margin = new Padding(3, 8, 3, 3);

            // Resumen del período.
            var resumen = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 0, 6, 6) };
            resumen.Controls.AddRange([
                BloqueResumen("Vendido", lblVendido), BloqueResumen("Costo", lblCosto), BloqueResumen("Ganancia", lblGanancia),
                BloqueResumen("% sobre costo", lblPorcentaje), BloqueResumen("Ventas", lblVentas)
            ]);

            // Izquierda: por día o por mes. Derecha: productos.
            Estilos.ConfigurarGrilla(grillaPeriodos);
            grillaPeriodos.ReadOnly = true;
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.Etiqueta), "Período", llenar: true);
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.CantidadVentas), "Ventas", "N0", 80);
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.Vendido), "Vendido", "C2", 130);
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.Costo), "Costo", "C2", 130);
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.Ganancia), "Ganancia", "C2", 130);
            Estilos.Columna(grillaPeriodos, nameof(FilaDeReporte.PorcentajeSobreCosto), "%", "P0", 70);
            grillaPeriodos.CellFormatting += (_, e) => FormatearFila(grillaPeriodos, e);

            Estilos.ConfigurarGrilla(grillaProductos);
            grillaProductos.ReadOnly = true;
            Estilos.Columna(grillaProductos, nameof(RendimientoProducto.Nombre), "Producto", llenar: true);
            Estilos.Columna(grillaProductos, nameof(RendimientoProducto.Unidades), "Unidades", "N0", 90);
            Estilos.Columna(grillaProductos, nameof(RendimientoProducto.Vendido), "Vendido", "C2", 120);
            Estilos.Columna(grillaProductos, nameof(RendimientoProducto.Ganancia), "Ganancia", "C2", 120);
            Estilos.Columna(grillaProductos, nameof(RendimientoProducto.PorcentajeSobreCosto), "%", "P0", 70);
            grillaProductos.CellFormatting += (_, e) => FormatearFila(grillaProductos, e);

            var tituloProductos = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            tituloProductos.Controls.Add(Estilos.Etiqueta("Productos más vendidos, ordenados por", Estilos.Normal));
            tituloProductos.Controls.Add(cmbOrden);
            cmbOrden.Margin = new Padding(3, 4, 3, 3);

            var izquierda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            izquierda.Controls.Add(grillaPeriodos);
            var derecha = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            derecha.Controls.Add(grillaProductos);
            derecha.Controls.Add(tituloProductos);

            var columnas = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columnas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columnas.Controls.Add(izquierda, 0, 0);
            columnas.Controls.Add(derecha, 1, 0);

            Controls.Add(columnas);
            Controls.Add(resumen);
            Controls.Add(filtros);

            cmbPeriodo.SelectedIndexChanged += (_, _) => AplicarPeriodoRapido();
            dtpDesde.ValueChanged += (_, _) => FechasCambiadasAMano();
            dtpHasta.ValueChanged += (_, _) => FechasCambiadasAMano();
            cmbAgrupar.SelectedIndexChanged += (_, _) => Cargar();
            cmbOrden.SelectedIndexChanged += (_, _) => MostrarProductos();
        }

        public override string Titulo => "Reportes";

        private bool _yaSeMostro;

        public override void AlMostrar()
        {
            // La opción inicial se elige recién acá: las listas enlazadas a datos se llenan
            // cuando el control ya está dentro de la ventana, no en el constructor.
            if (!_yaSeMostro)
            {
                _yaSeMostro = true;
                Estilos.Elegir<OpcionPeriodo>(cmbPeriodo, p => p.Texto == "Este mes");
            }

            // Los períodos rápidos se recalculan por si cambió el día desde la última vez.
            AplicarPeriodoRapido();
            grillaPeriodos.Focus();
        }

        private Agrupacion AgrupacionElegida => cmbAgrupar.SelectedItem is Agrupacion a ? a : Agrupacion.PorDia;

        private OrdenProductos OrdenElegido => cmbOrden.SelectedItem is OrdenProductos o ? o : OrdenProductos.PorRecaudacion;

        private DateOnly Hoy => DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);

        private void AplicarPeriodoRapido()
        {
            if (cmbPeriodo.SelectedItem is not OpcionPeriodo { Calcular: { } calcular })
            {
                Cargar();
                return;
            }

            var periodo = calcular(Hoy);
            _actualizandoFechas = true;
            dtpDesde.Value = periodo.Desde.ToDateTime(TimeOnly.MinValue);
            dtpHasta.Value = periodo.Hasta.ToDateTime(TimeOnly.MinValue);
            _actualizandoFechas = false;

            // Para un año entero se ve mejor por mes; para lo demás, por día.
            cmbAgrupar.SelectedItem = periodo.Hasta.DayNumber - periodo.Desde.DayNumber > 62 ? Agrupacion.PorMes : Agrupacion.PorDia;
            Cargar();
        }

        private void FechasCambiadasAMano()
        {
            if (_actualizandoFechas)
                return;
            Estilos.Elegir<OpcionPeriodo>(cmbPeriodo, p => p.Calcular is null);
        }

        private void Cargar()
        {
            try
            {
                var periodo = new PeriodoReporte(DateOnly.FromDateTime(dtpDesde.Value), DateOnly.FromDateTime(dtpHasta.Value));
                _reporte = _reportes.Generar(periodo, AgrupacionElegida);
                MostrarEstado("", SystemColors.ControlText);
            }
            catch (ReglaDeNegocioException ex)
            {
                _reporte = null;
                MostrarEstado(ex.Message, Estilos.Error);
            }

            lblVendido.Text = _reporte?.Vendido.ToString("C2") ?? "-";
            lblCosto.Text = _reporte?.Costo.ToString("C2") ?? "-";
            lblGanancia.Text = _reporte?.Ganancia.ToString("C2") ?? "-";
            lblGanancia.ForeColor = _reporte?.Ganancia < 0 ? Estilos.Error : Estilos.Exito;
            lblPorcentaje.Text = _reporte?.PorcentajeSobreCosto?.ToString("P0") ?? "-";
            lblVentas.Text = _reporte?.CantidadVentas.ToString("N0") ?? "-";
            grillaPeriodos.DataSource = _reporte?.Filas.ToList();
            MostrarProductos();
        }

        private void MostrarProductos() =>
            grillaProductos.DataSource = _reporte?.ProductosOrdenados(OrdenElegido).ToList();

        // Pérdidas en rojo; períodos sin ventas, en gris.
        private static void FormatearFila(DataGridView grilla, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || grilla.Rows[e.RowIndex].DataBoundItem is not ConGanancia fila)
                return;
            if (fila.Vendido == 0)
                e.CellStyle!.ForeColor = SystemColors.GrayText;
            else if (grilla.Columns[e.ColumnIndex].DataPropertyName == nameof(ConGanancia.Ganancia) && fila.Ganancia < 0)
                e.CellStyle!.ForeColor = Estilos.Error;
        }

        private void ExportarPdf()
        {
            if (_reporte is null)
                return;

            Directory.CreateDirectory(ExportadorDeReportes.CarpetaPorDefecto);
            using var dialogo = new SaveFileDialog
            {
                Title = "Guardar el reporte en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                InitialDirectory = ExportadorDeReportes.CarpetaPorDefecto,
                FileName = ExportadorDeReportes.NombreDeArchivo(_reporte.Periodo)
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var ruta = _exportador.GuardarReporte(_reporte, OrdenElegido, dialogo.FileName);
                MostrarEstado($"PDF guardado: {Path.GetFileName(ruta)}", Estilos.Exito);
                Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MostrarEstado($"No se pudo guardar el PDF: {ex.Message}", Estilos.Error);
            }
        }

        private void MostrarEstado(string texto, Color color)
        {
            lblEstado.Text = texto;
            lblEstado.ForeColor = color;
        }

        private static Label Etiqueta(string texto) =>
            new() { Text = texto, AutoSize = true, Margin = new Padding(12, 11, 3, 3) };

        private static Label ValorResumen() => new() { AutoSize = true, Font = Estilos.Grande };

        private static Control BloqueResumen(string titulo, Label valor)
        {
            var bloque = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(12, 3, 36, 3) };
            bloque.Controls.Add(new Label { Text = titulo, AutoSize = true, ForeColor = SystemColors.GrayText });
            bloque.Controls.Add(valor);
            return bloque;
        }
    }
}
