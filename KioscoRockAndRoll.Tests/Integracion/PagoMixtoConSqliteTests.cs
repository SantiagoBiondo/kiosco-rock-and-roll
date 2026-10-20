using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class PagoMixtoConSqliteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly InventarioServicio _inventario;
    private readonly CajaServicio _caja;
    private readonly VentaServicio _ventas;
    private readonly Producto _coca;

    public PagoMixtoConSqliteTests()
    {
        var reloj = TimeProvider.System;
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        _caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        _ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), _caja, reloj);
        _coca = _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 50, 2));
        _caja.AbrirCaja(1000);
    }

    public void Dispose() => _base.Dispose();

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private Venta Vender(int unidades, Func<decimal, Cobro> cobrar)
    {
        var carrito = new Carrito();
        carrito.Agregar(_inventario.ObtenerPorId(_coca.Id)!, unidades);
        return _ventas.Cobrar(carrito, cobrar(carrito.Total));
    }

    [Fact]
    public void VentaMixta_SeGuardaConSusPagos()
    {
        // 2 cocas = 3000: 1000 con débito y 2000 en efectivo.
        var venta = Vender(2, total => Cobro.Combinado(total, [new Pago(MedioDePago.Debito, 1000)]));

        var detalle = _ventas.Detalle(venta.Id)!.Venta;

        Assert.Equal(
            [new Pago(MedioDePago.Efectivo, 2000), new Pago(MedioDePago.Debito, 1000)],
            detalle.PagosRealizados);
        Assert.Equal(MedioDePago.Efectivo, detalle.MedioDePago); // el principal
        Assert.Contains("+", detalle.MedioDePagoDescripcion);
    }

    [Fact]
    public void Caja_SumaCadaParteEnSuMedio_YCuentaLaVentaUnaSolaVez()
    {
        Vender(2, total => Cobro.Combinado(total, [new Pago(MedioDePago.Debito, 1000)])); // 2000 efvo + 1000 débito
        Vender(1, total => Cobro.Crear(total, MedioDePago.TransferenciaQr));               // 1500 QR

        var resumen = _caja.Resumen(_caja.TurnoAbierto!);

        Assert.Equal(1000 + 2000, resumen.EfectivoEsperado); // solo la parte en efectivo
        Assert.Equal(4500, resumen.TotalVendido);
        Assert.Equal(2, resumen.CantidadVentas);
        Assert.Equal(1000, resumen.Ventas.Single(v => v.Medio == MedioDePago.Debito).Total);
        Assert.Equal(1500, resumen.Ventas.Single(v => v.Medio == MedioDePago.TransferenciaQr).Total);
    }

    [Fact]
    public void ResumenDelDia_TotalPorMedio_ConVentasMixtas()
    {
        Vender(2, total => Cobro.Combinado(total, [new Pago(MedioDePago.Credito, 500)]));

        var dia = _ventas.ResumenDelDia(Hoy);

        Assert.Equal(2500, dia.TotalPorMedio[MedioDePago.Efectivo]);
        Assert.Equal(500, dia.TotalPorMedio[MedioDePago.Credito]);
        Assert.Equal(3000, dia.Total);
    }

    [Fact]
    public void VentaMixtaAnulada_NoCuentaEnNingunMedio()
    {
        var venta = Vender(2, total => Cobro.Combinado(total, [new Pago(MedioDePago.Debito, 1000)]));
        _ventas.Anular(venta.Id, "Error");

        var resumen = _caja.Resumen(_caja.TurnoAbierto!);

        Assert.Equal(1000, resumen.EfectivoEsperado);
        Assert.Equal(0, resumen.TotalVendido);
        Assert.Equal(0, resumen.CantidadVentas);
        Assert.Empty(_ventas.ResumenDelDia(Hoy).TotalPorMedio);
    }

    [Fact]
    public void Migracion_PasaLasVentasViejasAUnPagoPorElTotal()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            // Una base en la versión 6: las ventas tienen un solo medio, sin tabla de pagos.
            var conexion = new ConexionSqlite(Path.Combine(carpeta, "v6.db"));
            using (var db = conexion.Abrir())
            using (var comando = db.CreateCommand())
            {
                comando.CommandText = string.Join(";\n", InicializadorBaseDeDatos.Migraciones.Take(6)) + """
                    ;
                    PRAGMA user_version = 6;
                    INSERT INTO Productos (CodigoBarras, Nombre, PrecioCostoCentavos, PrecioVentaCentavos, Stock, StockMinimo)
                    VALUES ('1', 'Coca', 90000, 150000, 10, 2);
                    INSERT INTO Ventas (Fecha, MedioPago) VALUES ('2026-10-01 10:00:00', 2);
                    INSERT INTO VentaLineas (VentaId, ProductoId, NombreProducto, Cantidad, PrecioUnitarioCentavos, CostoUnitarioCentavos)
                    VALUES (1, 1, 'Coca', 2, 150000, 90000);
                    """;
                comando.ExecuteNonQuery();
            }

            new InicializadorBaseDeDatos(conexion).Inicializar();

            var detalle = new SqliteVentaRepositorio(conexion).ObtenerDetalle(1)!;
            Assert.Equal([new Pago(MedioDePago.Debito, 3000)], detalle.Venta.Pagos);
        }
        finally
        {
            try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { }
        }
    }
}
