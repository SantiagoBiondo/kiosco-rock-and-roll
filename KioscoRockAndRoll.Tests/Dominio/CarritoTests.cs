using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class CarritoTests
{
    private static Producto Guardado(long id, decimal precio, int stock = 100) =>
        Producto.Restaurar(id, $"COD{id}", $"Producto {id}", precio / 2, precio, stock, 0);

    [Fact]
    public void Agregar_MismoProducto_SumaEnElMismoRenglon()
    {
        var carrito = new Carrito();
        var coca = Guardado(1, 800);

        carrito.Agregar(coca);
        carrito.Agregar(coca, 2);

        var item = Assert.Single(carrito.Items);
        Assert.Equal(3, item.Cantidad);
        Assert.Equal(2400, carrito.Total);
    }

    [Fact]
    public void Total_SumaTodosLosRenglones()
    {
        var carrito = new Carrito();
        carrito.Agregar(Guardado(1, 800), 2);
        carrito.Agregar(Guardado(2, 350.50m), 1);

        Assert.Equal(1950.50m, carrito.Total);
        Assert.Equal(3, carrito.Unidades);
    }

    [Fact]
    public void Agregar_ProductoSinGuardar_Falla()
    {
        var carrito = new Carrito();
        Assert.Throws<InvalidOperationException>(() => carrito.Agregar(new Producto("1", "X", 1, 1)));
    }

    [Fact]
    public void Agregar_CantidadCero_Falla()
    {
        var carrito = new Carrito();
        Assert.Throws<ReglaDeNegocioException>(() => carrito.Agregar(Guardado(1, 100), 0));
    }

    [Fact]
    public void CambiarCantidad_ACero_QuitaElRenglon()
    {
        var carrito = new Carrito();
        carrito.Agregar(Guardado(1, 100));

        carrito.CambiarCantidad(1, 0);

        Assert.True(carrito.EstaVacio);
    }

    [Fact]
    public void SuperaElStock_CuandoSeLlevanMasDeLasQueHay()
    {
        var carrito = new Carrito();
        var item = carrito.Agregar(Guardado(1, 100, stock: 2), 3);

        Assert.True(item.SuperaElStock);
    }

    [Fact]
    public void Cambio_SeDisparaAlModificar()
    {
        var carrito = new Carrito();
        var avisos = 0;
        carrito.Cambio += (_, _) => avisos++;

        carrito.Agregar(Guardado(1, 100));
        carrito.CambiarCantidad(1, 5);
        carrito.Quitar(1);
        carrito.Vaciar(); // ya está vacío: no avisa

        Assert.Equal(3, avisos);
    }

    [Fact]
    public void Refrescar_ActualizaPreciosYQuitaLosQueNoExisten()
    {
        var carrito = new Carrito();
        carrito.Agregar(Guardado(1, 100));
        carrito.Agregar(Guardado(2, 200));

        carrito.Refrescar(id => id == 1 ? Guardado(1, 150) : null);

        var item = Assert.Single(carrito.Items);
        Assert.Equal(150, item.PrecioUnitario);
    }
}
