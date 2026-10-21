using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class TicketConSqliteTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public void Configuracion_SinNadaGuardado_EsLaDeSiempre()
    {
        Assert.Equal(ConfiguracionDeTicket.PorDefecto, new AjustesDeTicket(new Ajustes(_base.Conexion)).Leer());
    }

    [Fact]
    public void Configuracion_SeGuardaYSeLee()
    {
        var ajustes = new AjustesDeTicket(new Ajustes(_base.Conexion));
        var configuracion = new ConfiguracionDeTicket("POS-80", AnchoDePapel.Rollo80mm, ImprimirAlCobrar: true);

        ajustes.Guardar(configuracion);

        Assert.Equal(configuracion, new AjustesDeTicket(new Ajustes(_base.Conexion)).Leer());
    }

    [Fact]
    public void Configuracion_SinImpresora_BorraLaAnterior()
    {
        var ajustes = new AjustesDeTicket(new Ajustes(_base.Conexion));
        ajustes.Guardar(new ConfiguracionDeTicket("POS-58", AnchoDePapel.Rollo58mm, true));

        ajustes.Guardar(new ConfiguracionDeTicket(null, AnchoDePapel.Rollo58mm, false));

        Assert.False(ajustes.Leer().HayImpresora);
    }

    [Fact]
    public void TemaOscuro_PorDefectoNo_YSeGuarda()
    {
        var apariencia = new AjustesDeApariencia(new Ajustes(_base.Conexion));
        Assert.False(apariencia.TemaOscuro);

        apariencia.TemaOscuro = true;

        Assert.True(new AjustesDeApariencia(new Ajustes(_base.Conexion)).TemaOscuro);
    }

    [Fact]
    public void Ticket_DeUnaVentaReal_YSuCopia()
    {
        var reloj = TimeProvider.System;
        var inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        var caja = new CajaServicio(new SqliteCajaRepositorio(_base.Conexion), reloj);
        var ventas = new VentaServicio(new SqliteVentaRepositorio(_base.Conexion), caja, reloj);
        var coca = inventario.Crear(new DatosProducto("1", "Coca", 900, 1500, 10, 2));
        caja.AbrirCaja(0);
        var carrito = new Carrito();
        carrito.Agregar(coca, 2);
        var cobro = Cobro.Combinado(carrito.Total, [new Pago(MedioDePago.Debito, 1000)], efectivoRecibido: 2500);
        var venta = ventas.Cobrar(carrito, cobro);

        var ticket = Ticket.DeVenta(venta, cobro, "Kiosco");
        var copia = Ticket.CopiaDe(ventas.Detalle(venta.Id)!, "Kiosco");

        Assert.Equal(3000, ticket.Total);
        Assert.Equal(500, ticket.Vuelto);           // pagó 2000 en efectivo con 2500
        Assert.Equal(3000, copia.Total);
        Assert.True(copia.EsCopia);
        Assert.Null(copia.Vuelto);                  // en la copia no se sabe con cuánto pagó
        Assert.Equal(ticket.Pagos, copia.Pagos);
    }
}
