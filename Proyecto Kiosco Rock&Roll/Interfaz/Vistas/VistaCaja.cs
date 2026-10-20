using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Controles;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas
{
    /// <summary>
    /// Caja: abrir y cerrar el turno, registrar ingresos y retiros de efectivo,
    /// ver los cierres anteriores y las ventas de cualquier día.
    /// </summary>
    public class VistaCaja : Vista
    {
        private readonly CajaServicio _caja;

        private readonly PanelLateral panelLateral = new(ancho: 500);
        private readonly PanelAbrirCaja panelAbrir;
        private readonly PanelMovimientoCaja panelMovimiento;
        private readonly PanelCerrarCaja panelCerrar;
        private readonly PanelDetalleVenta panelDetalle;
        private readonly ListadoVentasPorDia ventasPorDia;

        private readonly Label lblEstado = new() { AutoSize = true, Font = Estilos.Titulo, Margin = new Padding(6, 12, 24, 3) };
        private readonly Label lblAviso = new() { AutoSize = true, Margin = new Padding(12, 16, 3, 3), MaximumSize = new Size(800, 0) };
        private readonly Button btnAbrir;
        private readonly Button btnIngreso;
        private readonly Button btnRetiro;
        private readonly Button btnCerrar;

        private readonly TabControl pestanias = new() { Dock = DockStyle.Fill, Font = Estilos.Normal, Padding = new Point(16, 6) };
        private readonly TabPage tabTurno = new("Turno actual");

        // Cuentas del turno (actual o último cerrado).
        private readonly Label lblTituloResumen = Estilos.Etiqueta("", Estilos.Titulo);
        private readonly Label lblInicial = ValorResumen();
        private readonly Label lblVentasEfectivo = ValorResumen();
        private readonly Label lblIngresos = ValorResumen();
        private readonly Label lblRetiros = ValorResumen();
        private readonly Label lblEsperado = ValorResumen(Estilos.Grande);
        private readonly Label lblContado = ValorResumen(Estilos.Grande);
        private readonly Label lblDiferencia = ValorResumen(Estilos.Grande);
        private readonly Label lblOtrosMedios = ValorResumen();
        private readonly Label lblTotalVendido = ValorResumen();
        private readonly Control[] filasDeCierre;
        private readonly DataGridView grillaMovimientos = new();
        private readonly DataGridView grillaTurnos = new();

        public VistaCaja(CajaServicio caja, ListadoVentasPorDia ventasPorDia,
            PanelAbrirCaja panelAbrir, PanelMovimientoCaja panelMovimiento, PanelCerrarCaja panelCerrar,
            PanelDetalleVenta panelDetalle)
        {
            _caja = caja;
            this.ventasPorDia = ventasPorDia;
            this.panelAbrir = panelAbrir;
            this.panelMovimiento = panelMovimiento;
            this.panelCerrar = panelCerrar;
            this.panelDetalle = panelDetalle;

            // Barra de arriba: estado y acciones.
            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            arriba.Controls.Add(lblEstado);
            btnAbrir = Estilos.Boton("Abrir caja", (_, _) => AbrirPanel(panelAbrir, () => panelAbrir.Iniciar()), 150);
            btnIngreso = Estilos.Boton("Ingreso de efectivo", (_, _) => AbrirPanel(panelMovimiento, () => panelMovimiento.Iniciar(TipoMovimientoCaja.Ingreso)), 190);
            btnRetiro = Estilos.Boton("Retiro de efectivo", (_, _) => AbrirPanel(panelMovimiento, () => panelMovimiento.Iniciar(TipoMovimientoCaja.Retiro)), 190);
            btnCerrar = Estilos.Boton("Cerrar caja", (_, _) => AbrirPanel(panelCerrar, panelCerrar.Iniciar), 150);
            arriba.Controls.AddRange([btnAbrir, btnIngreso, btnRetiro, btnCerrar, lblAviso]);

            // Pestaña "Turno actual": las cuentas a la izquierda, los movimientos a la derecha.
            // La tabla va arriba dentro de una columna fija: si la tabla misma ocupara todo el alto,
            // la última fila se quedaría con el espacio sobrante y su texto aparecería muy abajo.
            var cuentas = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
            var columnaCuentas = new Panel { Dock = DockStyle.Left, Width = 460 };
            columnaCuentas.Controls.Add(cuentas);
            cuentas.Controls.Add(lblTituloResumen);
            cuentas.SetColumnSpan(lblTituloResumen, 2);
            AgregarFila(cuentas, "Efectivo inicial", lblInicial);
            AgregarFila(cuentas, "+ Ventas en efectivo", lblVentasEfectivo);
            AgregarFila(cuentas, "+ Ingresos de efectivo", lblIngresos);
            AgregarFila(cuentas, "− Retiros de efectivo", lblRetiros);
            AgregarFila(cuentas, "= Efectivo esperado", lblEsperado, Estilos.Grande);
            var filaContado = AgregarFila(cuentas, "Efectivo contado", lblContado, Estilos.Grande);
            var filaDiferencia = AgregarFila(cuentas, "Diferencia", lblDiferencia, Estilos.Grande);
            filasDeCierre = [filaContado, lblContado, filaDiferencia, lblDiferencia];
            AgregarFila(cuentas, "Otros medios de pago", lblOtrosMedios);
            AgregarFila(cuentas, "Total vendido", lblTotalVendido);

            Estilos.ConfigurarGrilla(grillaMovimientos);
            grillaMovimientos.ReadOnly = true;
            Estilos.Columna(grillaMovimientos, nameof(MovimientoCaja.Fecha), "Hora", "HH:mm", 80);
            Estilos.Columna(grillaMovimientos, nameof(MovimientoCaja.TipoDescripcion), "Tipo", ancho: 100);
            Estilos.Columna(grillaMovimientos, nameof(MovimientoCaja.Monto), "Monto", "C2", 130);
            Estilos.Columna(grillaMovimientos, nameof(MovimientoCaja.Motivo), "Motivo", llenar: true);
            var movimientos = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            movimientos.Controls.Add(grillaMovimientos);
            movimientos.Controls.Add(new Label { Text = "Ingresos y retiros de efectivo del turno", Dock = DockStyle.Top, Height = 30 });

            tabTurno.Controls.Add(movimientos);
            tabTurno.Controls.Add(columnaCuentas);

            // Pestaña "Turnos anteriores".
            Estilos.ConfigurarGrilla(grillaTurnos);
            grillaTurnos.ReadOnly = true;
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.Apertura), "Apertura", "dd/MM/yyyy HH:mm", 170);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.Cierre), "Cierre", "dd/MM HH:mm", 130);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.CantidadVentas), "Ventas", "N0", 80);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.TotalVendido), "Total vendido", "C2", 150);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.EfectivoEsperado), "Efectivo esperado", "C2", 170);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.EfectivoContado), "Contado", "C2", 140);
            Estilos.Columna(grillaTurnos, nameof(ResumenTurno.Diferencia), "Diferencia", llenar: true);
            grillaTurnos.CellFormatting += FormatearDiferencia;
            var tabAnteriores = new TabPage("Turnos anteriores");
            tabAnteriores.Controls.Add(grillaTurnos);

            var tabVentas = new TabPage("Ventas por día");
            ventasPorDia.Dock = DockStyle.Fill;
            tabVentas.Controls.Add(ventasPorDia);

            pestanias.TabPages.AddRange([tabTurno, tabAnteriores, tabVentas]);
            pestanias.SelectedIndexChanged += (_, _) => Cargar();

            var principal = new Panel { Dock = DockStyle.Fill };
            principal.Controls.Add(pestanias);
            principal.Controls.Add(arriba);
            Controls.Add(principal);
            Controls.Add(panelLateral);

            panelAbrir.Abierta += (_, _) => CerrarPanel();
            panelAbrir.Cancelado += (_, _) => CerrarPanel();
            panelMovimiento.Registrado += (_, _) => CerrarPanel();
            panelMovimiento.Cancelado += (_, _) => CerrarPanel();
            panelCerrar.Cerrada += (_, cierre) =>
            {
                CerrarPanel();
                if (cierre.RutaPdf is { } ruta)
                    MostrarAviso($"PDF guardado en {ruta}", Estilos.Exito);
                else if (cierre.ErrorPdf is { } error)
                    MostrarAviso($"La caja se cerró, pero no se pudo guardar el PDF: {error}", Color.Red);
            };
            panelCerrar.Cancelado += (_, _) => CerrarPanel();

            // Detalle y anulación desde la pestaña "Ventas por día": al terminar se sigue en esa pestaña.
            ventasPorDia.VentaElegida += (_, venta) => AbrirPanel(panelDetalle, () => panelDetalle.Mostrar(venta.Id));
            panelDetalle.Anulada += (_, _) => CerrarPanel(volverAlTurno: false);
            panelDetalle.Cerrado += (_, _) => CerrarPanel(volverAlTurno: false);
        }

        public override string Titulo => "Caja";
        public override Keys Atajo => Keys.F4;

        public override void AlMostrar()
        {
            Cargar();
            if (panelLateral.Contenido is { } panel)
                panel.SelectNextControl(panel, true, true, true, true);
            else
                pestanias.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                panelAbrir.Dispose();
                panelMovimiento.Dispose();
                panelCerrar.Dispose();
                panelDetalle.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Label ValorResumen(Font? fuente = null) =>
            new() { AutoSize = true, Font = fuente ?? Estilos.Normal, Anchor = AnchorStyles.Right, Margin = new Padding(24, 6, 3, 6) };

        private static Label AgregarFila(TableLayoutPanel tabla, string concepto, Label valor, Font? fuente = null)
        {
            var etiqueta = Estilos.Etiqueta(concepto, fuente);
            tabla.Controls.Add(etiqueta);
            tabla.Controls.Add(valor);
            return etiqueta;
        }

        private void Cargar()
        {
            var abierto = _caja.TurnoAbierto;
            btnAbrir.Enabled = abierto is null;
            btnIngreso.Enabled = btnRetiro.Enabled = btnCerrar.Enabled = abierto is not null;

            var anteriores = _caja.TurnosAnteriores();
            lblEstado.Text = abierto is not null
                ? $"Caja abierta desde el {abierto.Apertura:dd/MM HH:mm}"
                : "Caja cerrada";
            lblEstado.ForeColor = abierto is not null ? Estilos.Exito : Estilos.Advertencia;

            // Si está abierta se ven las cuentas del turno; si no, las del último cierre.
            var resumen = abierto is not null ? _caja.Resumen(abierto) : anteriores.FirstOrDefault();
            MostrarResumen(resumen);

            switch (pestanias.SelectedIndex)
            {
                case 1: grillaTurnos.DataSource = anteriores.ToList(); break;
                case 2: ventasPorDia.Cargar(); break;
            }
        }

        private void MostrarResumen(ResumenTurno? resumen)
        {
            if (resumen is null)
            {
                lblTituloResumen.Text = "Todavía no hay turnos. Abrí la caja para empezar.";
                foreach (var valor in new[] { lblInicial, lblVentasEfectivo, lblIngresos, lblRetiros, lblEsperado, lblOtrosMedios, lblTotalVendido })
                    valor.Text = "-";
                foreach (var fila in filasDeCierre)
                    fila.Visible = false;
                grillaMovimientos.DataSource = null;
                return;
            }

            var cerrado = !resumen.Turno.EstaAbierto;
            lblTituloResumen.Text = cerrado
                ? $"Último turno ({resumen.Apertura:dd/MM HH:mm} a {resumen.Cierre:HH:mm})"
                : "Turno actual";
            lblInicial.Text = resumen.EfectivoInicial.ToString("C2");
            lblVentasEfectivo.Text = $"{resumen.VentasEnEfectivo:C2}";
            lblIngresos.Text = resumen.Ingresos.ToString("C2");
            lblRetiros.Text = resumen.Retiros.ToString("C2");
            lblEsperado.Text = resumen.EfectivoEsperado.ToString("C2");

            foreach (var fila in filasDeCierre)
                fila.Visible = cerrado;
            if (cerrado)
            {
                lblContado.Text = resumen.EfectivoContado?.ToString("C2");
                var diferencia = resumen.Diferencia ?? 0;
                lblDiferencia.Text = ResumenTurno.DescribirDiferencia(diferencia);
                lblDiferencia.ForeColor = diferencia == 0 ? Estilos.Exito : diferencia > 0 ? Estilos.Advertencia : Color.Red;
            }

            var otros = resumen.Ventas.Where(v => v.Medio != MedioDePago.Efectivo).ToList();
            lblOtrosMedios.Text = otros.Count == 0
                ? "-"
                : string.Join("\n", otros.Select(v => $"{v.Medio.Descripcion()}: {v.Total:C2}"));
            lblTotalVendido.Text = $"{resumen.TotalVendido:C2}  ({resumen.CantidadVentas} ventas)";

            grillaMovimientos.DataSource = resumen.Movimientos.ToList();
        }

        private void FormatearDiferencia(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (grillaTurnos.Columns[e.ColumnIndex].DataPropertyName != nameof(ResumenTurno.Diferencia) || e.Value is not decimal diferencia)
                return;
            e.Value = ResumenTurno.DescribirDiferencia(diferencia);
            e.CellStyle!.ForeColor = diferencia == 0 ? Estilos.Exito : diferencia > 0 ? Estilos.Advertencia : Color.Red;
            e.FormattingApplied = true;
        }

        private void MostrarAviso(string texto, Color color)
        {
            lblAviso.Text = texto;
            lblAviso.ForeColor = color;
        }

        private void AbrirPanel(Control panel, Action iniciar)
        {
            MostrarAviso("", SystemColors.ControlText);
            panelLateral.Mostrar(panel);
            iniciar();
        }

        private void CerrarPanel(bool volverAlTurno = true)
        {
            panelLateral.Ocultar();
            if (volverAlTurno)
                pestanias.SelectedTab = tabTurno;
            Cargar();
            pestanias.SelectedTab?.SelectNextControl(pestanias.SelectedTab, true, true, true, true);
        }
    }
}
