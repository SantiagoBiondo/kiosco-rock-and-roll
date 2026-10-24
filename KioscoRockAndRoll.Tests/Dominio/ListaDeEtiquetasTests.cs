using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class ListaDeEtiquetasTests
{
    private static Producto Producto(long id, string nombre, decimal precio) =>
        KioscoRockAndRoll.Dominio.Entidades.Producto.Restaurar(id, $"c{id}", nombre, 1, precio, 0, 0);

    private readonly Producto _coca = Producto(1, "Coca", 1650);
    private readonly Producto _alfajor = Producto(2, "Alfajor", 880);

    [Fact]
    public void EscanearDosVeces_SumaEnElMismoRenglon()
    {
        var lista = new ListaDeEtiquetas();

        lista.Agregar(_coca);
        lista.Agregar(_alfajor, 3);
        lista.Agregar(_coca);

        Assert.Equal(2, lista.Pedidas.Count);
        Assert.Equal(2, lista.Buscar(_coca.Id)!.Cantidad);
        Assert.Equal(5, lista.Total);
    }

    [Fact]
    public void Etiquetas_RespetaElOrdenYLasCantidades()
    {
        var lista = new ListaDeEtiquetas();
        lista.Agregar(_coca, 2);
        lista.Agregar(_alfajor);

        Assert.Equal(
            [new Etiqueta("Coca", 1650), new Etiqueta("Coca", 1650), new Etiqueta("Alfajor", 880)],
            lista.Etiquetas());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(48, 2)]
    public void Hojas(int etiquetas, int hojas)
    {
        var lista = new ListaDeEtiquetas();
        if (etiquetas > 0)
            lista.Agregar(_coca, etiquetas);

        Assert.Equal(hojas, lista.Hojas(24));
    }

    [Fact]
    public void CambiarCantidadACero_LoQuita()
    {
        var lista = new ListaDeEtiquetas();
        lista.Agregar(_coca, 2);

        lista.CambiarCantidad(_coca.Id, 0);

        Assert.True(lista.EstaVacia);
    }

    [Fact]
    public void Refrescar_TomaLosPreciosNuevosYQuitaLosBorrados()
    {
        var lista = new ListaDeEtiquetas();
        lista.Agregar(_coca);
        lista.Agregar(_alfajor);
        var cocaAumentada = Producto(1, "Coca", 1800);

        lista.Refrescar(id => id == _coca.Id ? cocaAumentada : null);

        Assert.Equal([new Etiqueta("Coca", 1800)], lista.Etiquetas());
    }

    [Fact]
    public void AvisaCadaCambio()
    {
        var lista = new ListaDeEtiquetas();
        var cambios = 0;
        lista.Cambio += (_, _) => cambios++;

        lista.Agregar(_coca);
        lista.CambiarCantidad(_coca.Id, 4);
        lista.Quitar(_coca.Id);
        lista.Vaciar(); // ya estaba vacía: no avisa

        Assert.Equal(3, cambios);
    }

    [Fact]
    public void CantidadInvalida_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => new ListaDeEtiquetas().Agregar(_coca, 0));
    }
}
