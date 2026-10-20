using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class ElementoDeCatalogoTests
{
    [Fact]
    public void Nombre_SeRecorta()
    {
        Assert.Equal("Bebidas", new Categoria("  Bebidas ").Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nombre_Vacio_Falla(string nombre)
    {
        Assert.Throws<ReglaDeNegocioException>(() => new Categoria(nombre));
    }

    [Fact]
    public void Nombre_DemasiadoLargo_Falla()
    {
        var largo = new string('x', ElementoDeCatalogo.LargoMaximoNombre + 1);
        Assert.Throws<ReglaDeNegocioException>(() => new Proveedor(largo));
    }

    [Fact]
    public void Proveedor_TelefonoOpcional()
    {
        Assert.Equal("", new Proveedor("Distribuidora Sur").Telefono);
        Assert.Equal("11 5555-1234", new Proveedor("Distribuidora Sur", " 11 5555-1234 ").Telefono);
    }
}

public class FiltroProductosTests
{
    private static Producto Con(long? categoria, long? proveedor, int stock = 10, int minimo = 2) =>
        Producto.Restaurar(1, "1", "X", 1, 2, stock, minimo, categoria, proveedor);

    [Fact]
    public void Todos_AceptaCualquiera()
    {
        var filtro = new FiltroProductos();
        Assert.True(filtro.Acepta(Con(null, null)));
        Assert.True(filtro.Acepta(Con(3, 4)));
    }

    [Fact]
    public void SinAsignar_SoloLosQueNoTienen()
    {
        var filtro = new FiltroProductos { Categoria = FiltroClasificacion.SinAsignar };
        Assert.True(filtro.Acepta(Con(null, 4)));
        Assert.False(filtro.Acepta(Con(3, 4)));
    }

    [Fact]
    public void Solo_UnoEnParticular()
    {
        var filtro = new FiltroProductos { Proveedor = FiltroClasificacion.Solo(4) };
        Assert.True(filtro.Acepta(Con(null, 4)));
        Assert.False(filtro.Acepta(Con(null, 5)));
        Assert.False(filtro.Acepta(Con(null, null)));
    }

    [Fact]
    public void Combina_TodosLosCriterios()
    {
        var filtro = new FiltroProductos
        {
            SoloStockBajo = true,
            Categoria = FiltroClasificacion.Solo(3),
            Proveedor = FiltroClasificacion.Solo(4)
        };
        Assert.True(filtro.Acepta(Con(3, 4, stock: 1)));
        Assert.False(filtro.Acepta(Con(3, 4, stock: 10)));
        Assert.False(filtro.Acepta(Con(9, 4, stock: 1)));
    }

    [Fact]
    public void FiltrosIguales_SonIguales()
    {
        Assert.Equal(FiltroClasificacion.Solo(3), FiltroClasificacion.Solo(3));
        Assert.NotEqual(FiltroClasificacion.Todos, FiltroClasificacion.SinAsignar);
    }
}

public class PedidoAProveedorTests
{
    private static readonly Proveedor Sur = Proveedor.Restaurar(4, "Distribuidora Sur", "");

    private static Producto Producto(long id, string nombre, int stock, int minimo, long? proveedor = 4) =>
        KioscoRockAndRoll.Dominio.Entidades.Producto.Restaurar(id, $"C{id}", nombre, 1, 2, stock, minimo, null, proveedor);

    [Fact]
    public void Armar_SoloStockBajoDelProveedor_OrdenadoPorNombre()
    {
        var pedido = PedidoAProveedor.Armar(Sur,
        [
            Producto(1, "Coca", stock: 2, minimo: 6),
            Producto(2, "Alfajor", stock: 0, minimo: 10),
            Producto(3, "Chicle", stock: 50, minimo: 5),            // stock bien
            Producto(4, "Papas", stock: 1, minimo: 4, proveedor: 9)  // otro proveedor
        ]);

        Assert.Equal(["Alfajor", "Coca"], pedido.Lineas.Select(l => l.Nombre));
    }

    [Theory]
    [InlineData(2, 6, 10)]   // hasta el doble del mínimo: 12 - 2
    [InlineData(0, 10, 20)]
    [InlineData(-3, 5, 13)]  // stock negativo: también se repone
    [InlineData(0, 0, 1)]    // sin mínimo: al menos 1
    public void CantidadSugerida(int stock, int minimo, int esperada)
    {
        Assert.Equal(esperada, PedidoAProveedor.CantidadSugerida(Producto(1, "X", stock, minimo)));
    }

    [Fact]
    public void Texto_IncluyeSoloLoQueSePide()
    {
        var pedido = PedidoAProveedor.Armar(Sur,
            [Producto(1, "Coca", stock: 2, minimo: 6), Producto(2, "Alfajor", stock: 0, minimo: 10)]);
        pedido.Lineas.Single(l => l.Nombre == "Alfajor").Cantidad = 0;

        var texto = pedido.ATexto("Kiosco", new DateOnly(2026, 10, 20));

        Assert.Equal("Pedido de Kiosco - 20/10/2026\r\nProveedor: Distribuidora Sur\r\n\r\n- 10 x Coca".ReplaceLineEndings(), texto);
    }

    [Fact]
    public void Cantidad_Negativa_Falla()
    {
        var pedido = PedidoAProveedor.Armar(Sur, [Producto(1, "Coca", 0, 5)]);
        Assert.Throws<ReglaDeNegocioException>(() => pedido.Lineas[0].Cantidad = -1);
    }

    [Fact]
    public void EstaVacio_SiTodoEnCero()
    {
        var pedido = PedidoAProveedor.Armar(Sur, [Producto(1, "Coca", 0, 5)]);
        Assert.False(pedido.EstaVacio);

        pedido.Lineas[0].Cantidad = 0;

        Assert.True(pedido.EstaVacio);
    }
}
