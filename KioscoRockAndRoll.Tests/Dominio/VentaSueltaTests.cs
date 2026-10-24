using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class VentaSueltaTests
{
    private static Producto Guardado(long id, string codigo, string nombre, int stock = 0) =>
        Producto.Restaurar(id, codigo, nombre, 100, 200, stock, 0);

    [Theory]
    [InlineData(5, 1, 20, 3, 0)]   // alcanzan las sueltas
    [InlineData(0, 1, 20, 3, 1)]   // no hay sueltas: se abre uno
    [InlineData(2, 25, 20, 3, 2)]  // faltan 23: se abren dos
    [InlineData(0, 70, 20, 3, 3)]  // faltan 70 pero quedan 3 paquetes: no se abren más
    [InlineData(0, 1, 20, 0, 0)]   // no quedan paquetes cerrados
    [InlineData(-2, 1, 20, 3, 1)]  // con sueltas negativas, el paquete también cubre lo que faltaba
    public void PaquetesQueHayQueAbrir(int sueltas, int cantidad, int porPaquete, int cerrados, int esperado)
    {
        Assert.Equal(esperado, Producto.PaquetesQueHayQueAbrir(sueltas, cantidad, porPaquete, cerrados));
    }

    [Fact]
    public void StockDisponible_SumaLasUnidadesDeLosPaquetesCerrados()
    {
        var atado = Guardado(1, "779", "Marlboro x20", stock: 2);
        var suelto = Guardado(2, "101", "Cigarrillo suelto", stock: 3);

        suelto.VenderSueltoDe(atado, 20);

        Assert.True(suelto.SeVendeSuelto);
        Assert.Equal(43, suelto.StockDisponible);
        Assert.True(suelto.TieneStockPara(43));
        Assert.False(suelto.TieneStockPara(44));
    }

    [Fact]
    public void SinPaquete_DejaDeVenderseSuelto()
    {
        var suelto = Guardado(2, "101", "Cigarrillo suelto", stock: 3);
        suelto.VenderSueltoDe(Guardado(1, "779", "Marlboro x20", stock: 2), 20);

        suelto.VenderSueltoDe(null, 0);

        Assert.False(suelto.SeVendeSuelto);
        Assert.Equal(3, suelto.StockDisponible);
    }

    [Fact]
    public void NoPuedeSalirDeSiMismo()
    {
        var producto = Guardado(1, "779", "Marlboro x20");
        Assert.Throws<ReglaDeNegocioException>(() => producto.VenderSueltoDe(producto, 20));
    }

    [Fact]
    public void ElPaqueteNoPuedeSerOtraUnidadSuelta()
    {
        var atado = Guardado(1, "779", "Marlboro x20");
        var suelto = Guardado(2, "101", "Cigarrillo suelto");
        suelto.VenderSueltoDe(atado, 20);

        Assert.Throws<ReglaDeNegocioException>(() => Guardado(3, "102", "Otro").VenderSueltoDe(suelto, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ElPaqueteTieneQueTraerVariasUnidades(int unidades)
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            Guardado(2, "101", "Suelto").VenderSueltoDe(Guardado(1, "779", "Atado"), unidades));
    }
}
