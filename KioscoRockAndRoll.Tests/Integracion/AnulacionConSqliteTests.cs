using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class AnulacionConSqliteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly InventarioServicio _inventario;
    private readonly CajaServicio _caja;
    private readonly VentaServicio _ventas;
    private readonly Producto _coca;
    private readonly Producto _alfajor;

    public AnulacionConSqliteTests()
    {
        var reloj = TimeProvider.System;
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        _caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        _ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), _caja, reloj);
        _coca = _inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 10, 2));
        _alfajor = _inventario.Crear(new DatosProducto("2", "Alfajor", 400, 700, 20, 2));
        _caja.AbrirCaja(1000);
    }

    public void Dispose() => _base.Dispose();

    private Venta Vender(MedioDePago medio = MedioDePago.Efectivo)
    {
        var carrito = new Carrito();
        carrito.Agregar(_inventario.ObtenerPorId(_coca.Id)!, 2);
        carrito.Agregar(_inventario.ObtenerPorId(_alfajor.Id)!, 3);
        return _ventas.Cobrar(carrito, Cobro.Crear(carrito.Total, medio));
    }

    private int Stock(Producto producto) => _inventario.ObtenerPorId(producto.Id)!.Stock;

    private DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    [Fact]
    public void Detalle_TraeLosRenglones()
    {
        var venta = Vender();

        var detalle = _ventas.Detalle(venta.Id)!;

        Assert.Equal(5100, detalle.Venta.Total);
        Assert.Equal(["Coca", "Alfajor"], detalle.Lineas.Select(l => l.NombreProducto));
        Assert.Equal(2, detalle.Lineas[0].Cantidad);
    }

    [Fact]
    public void Anular_DevuelveElStock_YQuedaRegistrada()
    {
        var venta = Vender();
        Assert.Equal(8, Stock(_coca));
        Assert.Equal(17, Stock(_alfajor));

        _ventas.Anular(venta.Id, "Cobré de más");

        Assert.Equal(10, Stock(_coca));
        Assert.Equal(20, Stock(_alfajor));
        var anulada = _ventas.Detalle(venta.Id)!.Venta;
        Assert.True(anulada.Anulada);
        Assert.Equal("Cobré de más", anulada.MotivoAnulacion);
    }

    [Fact]
    public void Anulada_NoCuentaEnLaCaja_NiEnElTotalDelDia()
    {
        var anulada = Vender(MedioDePago.Efectivo);
        Vender(MedioDePago.Debito);

        _ventas.Anular(anulada.Id, "Error");

        var caja = _caja.Resumen(_caja.TurnoAbierto!);
        Assert.Equal(1000, caja.EfectivoEsperado);    // la venta en efectivo anulada no suma
        Assert.Equal(5100, caja.TotalVendido);         // solo la de débito
        Assert.Equal(1, caja.CantidadVentas);

        var dia = _ventas.ResumenDelDia(Hoy);
        Assert.Equal(2, dia.Ventas.Count);             // se listan las dos
        Assert.Equal(1, dia.CantidadAnuladas);
        Assert.Equal(5100, dia.Total);
        Assert.False(dia.TotalPorMedio.ContainsKey(MedioDePago.Efectivo));
    }

    [Fact]
    public void NoSeAnulaDosVeces_NiSeDevuelveElStockDosVeces()
    {
        var venta = Vender();
        _ventas.Anular(venta.Id, "Error");

        Assert.Throws<ReglaDeNegocioException>(() => _ventas.Anular(venta.Id, "Otra vez"));
        Assert.Equal(10, Stock(_coca));
    }

    [Fact]
    public void VentaDeUnTurnoCerrado_NoSePuedeAnular()
    {
        var venta = Vender();
        _caja.CerrarCaja(6100);
        _caja.AbrirCaja(0);

        Assert.Throws<ReglaDeNegocioException>(() => _ventas.Anular(venta.Id, "Error"));
        Assert.Equal(8, Stock(_coca));
    }

    [Fact]
    public void SinMotivo_NoSeAnula()
    {
        var venta = Vender();
        Assert.Throws<ReglaDeNegocioException>(() => _ventas.Anular(venta.Id, " "));
        Assert.False(_ventas.Detalle(venta.Id)!.Venta.Anulada);
    }

    [Fact]
    public void VentaInexistente_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => _ventas.Anular(999, "x"));
    }
}
