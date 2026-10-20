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
        Assert.Equal(1350, cobro.MontoRecibido);
        Assert.Equal(0, cobro.Vuelto);
    }

    [Fact]
    public void TotalCero_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Cobro.Crear(0, MedioDePago.Efectivo));
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
