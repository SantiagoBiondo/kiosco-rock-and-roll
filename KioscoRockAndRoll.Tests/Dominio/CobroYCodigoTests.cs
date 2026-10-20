using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class CobroTests
{
    [Fact]
    public void Efectivo_CalculaElVuelto()
    {
        var cobro = Cobro.Crear(1350, MedioDePago.Efectivo, 2000);
        Assert.Equal(650, cobro.Vuelto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Efectivo_SinMonto_PagaJusto(int? recibido)
    {
        var cobro = Cobro.Crear(1350, MedioDePago.Efectivo, recibido);
        Assert.Equal(0, cobro.Vuelto);
    }

    [Fact]
    public void Efectivo_MontoInsuficiente_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Cobro.Crear(1350, MedioDePago.Efectivo, 1000));
    }

    [Fact]
    public void Tarjeta_IgnoraElMontoRecibido()
    {
        var cobro = Cobro.Crear(1350, MedioDePago.Debito, 5000);
        Assert.Equal(0, cobro.EfectivoRecibido);
        Assert.Equal(0, cobro.Vuelto);
        Assert.Equal([new Pago(MedioDePago.Debito, 1350)], cobro.Pagos);
        Assert.False(cobro.EsMixto);
    }

    [Fact]
    public void TotalCero_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Cobro.Crear(0, MedioDePago.Efectivo));
    }

    // ---------- Varios medios ----------

    [Fact]
    public void Combinado_ElRestoVaEnEfectivo_ConVuelto()
    {
        var cobro = Cobro.Combinado(2650, [new Pago(MedioDePago.Debito, 1000)], efectivoRecibido: 2000);

        Assert.True(cobro.EsMixto);
        Assert.Equal([new Pago(MedioDePago.Efectivo, 1650), new Pago(MedioDePago.Debito, 1000)], cobro.Pagos);
        Assert.Equal(1650, cobro.EfectivoACobrar);
        Assert.Equal(350, cobro.Vuelto);
        Assert.Equal(MedioDePago.Efectivo, cobro.MedioPrincipal);
    }

    [Fact]
    public void Combinado_SinEfectivo_TarjetaYTransferencia()
    {
        var cobro = Cobro.Combinado(3000,
            [new Pago(MedioDePago.Credito, 2000), new Pago(MedioDePago.TransferenciaQr, 1000)]);

        Assert.Equal(2, cobro.Pagos.Count);
        Assert.Equal(0, cobro.EfectivoACobrar);
        Assert.Equal(0, cobro.Vuelto);
        Assert.Equal(MedioDePago.Credito, cobro.MedioPrincipal);
    }

    [Fact]
    public void Combinado_IgnoraLosMontosEnCero()
    {
        var cobro = Cobro.Combinado(1000,
            [new Pago(MedioDePago.Debito, 0), new Pago(MedioDePago.Credito, 1000), new Pago(MedioDePago.TransferenciaQr, 0)]);

        Assert.Equal([new Pago(MedioDePago.Credito, 1000)], cobro.Pagos);
        Assert.False(cobro.EsMixto);
    }

    [Fact]
    public void Combinado_TodoEnCero_EsTodoEfectivo()
    {
        var cobro = Cobro.Combinado(1000, [new Pago(MedioDePago.Debito, 0)]);
        Assert.Equal([new Pago(MedioDePago.Efectivo, 1000)], cobro.Pagos);
    }

    [Fact]
    public void Combinado_TarjetaMasQueElTotal_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            Cobro.Combinado(1000, [new Pago(MedioDePago.Debito, 800), new Pago(MedioDePago.Credito, 300)]));
    }

    [Fact]
    public void Combinado_EfectivoInsuficienteParaElResto_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            Cobro.Combinado(2000, [new Pago(MedioDePago.Debito, 500)], efectivoRecibido: 1000));
    }

    [Fact]
    public void Combinado_MontoNegativo_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            Cobro.Combinado(1000, [new Pago(MedioDePago.Debito, -100)]));
    }

    [Fact]
    public void Descripcion()
    {
        Assert.Equal("Débito", Cobro.Crear(1000, MedioDePago.Debito).Descripcion);
        var mixto = Cobro.Combinado(1500, [new Pago(MedioDePago.Debito, 1000)]).Descripcion;
        Assert.Contains("Efectivo", mixto);
        Assert.Contains(" + Débito", mixto);
    }
}

public class CodigoEscaneadoTests
{
    [Theory]
    [InlineData("7790895000997", "7790895000997", 1)]
    [InlineData("  7790895000997 ", "7790895000997", 1)]
    [InlineData("3*7790895000997", "7790895000997", 3)]
    [InlineData("12 * ABC", "ABC", 12)]
    public void Interpreta_CodigoYCantidad(string texto, string codigo, int cantidad)
    {
        Assert.True(CodigoEscaneado.TryInterpretar(texto, out var resultado));
        Assert.Equal(new CodigoEscaneado(codigo, cantidad), resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0*779")]
    [InlineData("x*779")]
    [InlineData("3*")]
    public void Rechaza_EntradasInvalidas(string? texto)
    {
        Assert.False(CodigoEscaneado.TryInterpretar(texto, out _));
    }
}
