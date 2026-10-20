using System.Text;
using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using KioscoRockAndRoll.Reportes;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class ReporteDelDiaTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly InventarioServicio _inventario;
    private readonly CajaServicio _caja;
    private readonly VentaServicio _ventas;
    private readonly ReporteDelDiaServicio _reportes;
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));

    public ReporteDelDiaTests()
    {
        var reloj = TimeProvider.System;
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        _caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        _ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), _caja, reloj);
        _reportes = new ReporteDelDiaServicio(_ventas, _caja);
    }

    public void Dispose()
    {
        _base.Dispose();
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private Venta Vender(params (Producto Producto, int Cantidad)[] items)
    {
        var carrito = new Carrito();
        foreach (var (producto, cantidad) in items)
            carrito.Agregar(_inventario.ObtenerPorId(producto.Id)!, cantidad);
        return _ventas.Cobrar(carrito, Cobro.Crear(carrito.Total, MedioDePago.Efectivo));
    }

    private (Producto Coca, Producto Alfajor) CargarProductosYVentas()
    {
        var coca = _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 50, 2));
        var alfajor = _inventario.Crear(new DatosProducto("2", "Alfajor", 400, 700, 50, 2));
        _caja.AbrirCaja(1000);
        Vender((coca, 2), (alfajor, 1));
        Vender((alfajor, 3));
        var anulada = Vender((coca, 10));
        _ventas.Anular(anulada.Id, "Error");
        _caja.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 500, "Pago a proveedor");
        return (coca, alfajor);
    }

    [Fact]
    public void ProductosVendidos_SumaPorProducto_SinAnuladas_DeMayorAMenor()
    {
        CargarProductosYVentas();

        var productos = _ventas.ProductosVendidos(Hoy);

        // Coca: 2 x 1500 = 3000 (la anulada no cuenta). Alfajor: 4 x 700 = 2800.
        Assert.Equal(
            [new ProductoVendido("Coca", 2, 3000), new ProductoVendido("Alfajor", 4, 2800)],
            productos);
    }

    [Fact]
    public void Datos_IncluyenLosTurnosDelDia()
    {
        CargarProductosYVentas();
        _caja.CerrarCaja(6300);
        _caja.AbrirCaja(0);

        var datos = _reportes.Datos(Hoy);

        Assert.Equal(2, datos.Turnos.Count);
        Assert.False(datos.Turnos[0].Turno.EstaAbierto);
        Assert.True(datos.Turnos[1].Turno.EstaAbierto);
        Assert.Equal(1000 + 5800 - 500, datos.Turnos[0].EfectivoEsperado);
        Assert.Equal(5800, datos.Ventas.Total);
        Assert.Empty(_reportes.Datos(Hoy.AddDays(-1)).Turnos);
    }

    [Fact]
    public void Pdf_SeGenera_ConDatos()
    {
        CargarProductosYVentas();
        var exportador = new ExportadorDeReportes(_reportes, TimeProvider.System);

        var pdf = exportador.VentasDelDia(Hoy);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Pdf_SeGenera_AunqueNoHayaVentas()
    {
        var exportador = new ExportadorDeReportes(_reportes, TimeProvider.System);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(exportador.VentasDelDia(Hoy), 0, 4));
    }

    [Fact]
    public void GuardarVentasDelDia_EscribeElArchivo_YCreaLaCarpeta()
    {
        CargarProductosYVentas();
        var exportador = new ExportadorDeReportes(_reportes, TimeProvider.System);
        var ruta = Path.Combine(_carpeta, "sub", ExportadorDeReportes.NombreDeArchivo(Hoy));

        var guardado = exportador.GuardarVentasDelDia(Hoy, ruta);

        Assert.Equal(ruta, guardado);
        Assert.True(new FileInfo(ruta).Length > 1000);
    }
}
