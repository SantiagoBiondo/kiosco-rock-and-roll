using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class ProductoTests
{
    [Fact]
    public void Crear_RecortaEspacios()
    {
        var producto = new Producto("  779123  ", "  Coca 500  ", 500, 800);

        Assert.Equal("779123", producto.CodigoBarras);
        Assert.Equal("Coca 500", producto.Nombre);
        Assert.True(producto.EsNuevo);
    }

    [Theory]
    [InlineData("", "Coca", 1, 1, 0)]
    [InlineData("779", " ", 1, 1, 0)]
    [InlineData("779", "Coca", -1, 1, 0)]
    [InlineData("779", "Coca", 1, -1, 0)]
    [InlineData("779", "Coca", 1, 1, -1)]
    public void Crear_ConDatosInvalidos_Falla(string codigo, string nombre, decimal costo, decimal venta, int minimo)
    {
        Assert.Throws<ReglaDeNegocioException>(() => new Producto(codigo, nombre, costo, venta, 0, minimo));
    }

    [Fact]
    public void IngresarYDescontarStock()
    {
        var producto = new Producto("779", "Coca", 500, 800, stock: 10);

        producto.IngresarStock(5);
        producto.DescontarStock(3);

        Assert.Equal(12, producto.Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void IngresarStock_CantidadNoPositiva_Falla(int cantidad)
    {
        var producto = new Producto("779", "Coca", 500, 800);
        Assert.Throws<ReglaDeNegocioException>(() => producto.IngresarStock(cantidad));
    }

    [Fact]
    public void DescontarStock_PuedeQuedarNegativo()
    {
        var producto = new Producto("779", "Coca", 500, 800, stock: 1);

        producto.DescontarStock(3);

        Assert.Equal(-2, producto.Stock);
        Assert.True(producto.StockBajo);
    }

    [Theory]
    [InlineData(2, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void StockBajo_SeCalculaContraElMinimo(int stock, int minimo, bool esperado)
    {
        var producto = new Producto("779", "Coca", 500, 800, stock, minimo);
        Assert.Equal(esperado, producto.StockBajo);
    }

    [Fact]
    public void AsignarId_SoloUnaVez()
    {
        var producto = new Producto("779", "Coca", 500, 800);
        producto.AsignarId(7);

        Assert.Equal(7, producto.Id);
        Assert.Throws<InvalidOperationException>(() => producto.AsignarId(8));
    }
}
