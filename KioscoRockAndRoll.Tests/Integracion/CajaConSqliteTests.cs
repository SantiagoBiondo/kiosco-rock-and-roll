using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;
using Microsoft.Data.Sqlite;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class CajaConSqliteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly CajaServicio _caja;
    private readonly VentaServicio _ventas;
    private readonly Producto _coca;

    public CajaConSqliteTests()
    {
        var reloj = TimeProvider.System;
        _caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        _ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), _caja, reloj);
        _coca = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion))
            .Crear(new DatosProducto("779", "Coca", 900, 1500, 100, 2));
    }

    public void Dispose() => _base.Dispose();

    private void Vender(int unidades, MedioDePago medio)
    {
        var carrito = new Carrito();
        carrito.Agregar(_coca, unidades);
        _ventas.Cobrar(carrito, Cobro.Crear(carrito.Total, medio));
    }

    [Fact]
    public void SinCajaAbierta_NoSePuedeCobrar()
    {
        Assert.Null(_caja.TurnoAbierto);
        Assert.Throws<ReglaDeNegocioException>(() => Vender(1, MedioDePago.Efectivo));
    }

    [Fact]
    public void NoSePuedenAbrirDosCajas()
    {
        _caja.AbrirCaja(1000);
        Assert.Throws<ReglaDeNegocioException>(() => _caja.AbrirCaja(1000));
    }

    [Fact]
    public void LaBaseTambienImpideDosTurnosAbiertos()
    {
        // Aunque alguien se saltee el servicio, el índice único de la base lo impide.
        var repositorio = new SqliteCajaRepositorio(_base.Conexion);
        repositorio.Agregar(Turno.Abrir(0, DateTime.Now));
        Assert.Throws<SqliteException>(() => repositorio.Agregar(Turno.Abrir(0, DateTime.Now)));
    }

    [Fact]
    public void TurnoCompleto_VentasMovimientosYCierre()
    {
        _caja.AbrirCaja(5000);
        Vender(2, MedioDePago.Efectivo);   // 3000 en efectivo
        Vender(1, MedioDePago.Debito);     // 1500 con débito
        _caja.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 2000, "Pago a proveedor");
        _caja.RegistrarMovimiento(TipoMovimientoCaja.Ingreso, 500, "Cambio");

        var parcial = _caja.Resumen(_caja.TurnoAbierto!);
        Assert.Equal(5000 + 3000 + 500 - 2000, parcial.EfectivoEsperado);
        Assert.Equal(4500, parcial.TotalVendido);
        Assert.Equal(2, parcial.CantidadVentas);
        Assert.Equal(2, parcial.Movimientos.Count);

        var cierre = _caja.CerrarCaja(6450);

        Assert.Null(_caja.TurnoAbierto);
        Assert.Equal(-50, cierre.Diferencia);
        var anterior = Assert.Single(_caja.TurnosAnteriores());
        Assert.Equal(6450, anterior.EfectivoContado);
        Assert.Equal(-50, anterior.Diferencia);
        Assert.Equal("Pago a proveedor", anterior.Movimientos[0].Motivo);
    }

    [Fact]
    public void LasVentasDeOtroTurnoNoSeMezclan()
    {
        _caja.AbrirCaja(0);
        Vender(1, MedioDePago.Efectivo);
        _caja.CerrarCaja(1500);

        _caja.AbrirCaja(0);
        Vender(3, MedioDePago.Efectivo);

        var actual = _caja.Resumen(_caja.TurnoAbierto!);
        Assert.Equal(4500, actual.VentasEnEfectivo);
        Assert.Equal(1500, Assert.Single(_caja.TurnosAnteriores()).VentasEnEfectivo);
    }

    [Fact]
    public void MovimientoConCajaCerrada_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            _caja.RegistrarMovimiento(TipoMovimientoCaja.Retiro, 100, "x"));
    }
}
