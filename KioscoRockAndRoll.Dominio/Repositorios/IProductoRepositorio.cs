using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Repositorios;

/// <summary>
/// Dónde se guardan los productos. El dominio no sabe si es SQLite, un archivo u otra cosa.
/// </summary>
public interface IProductoRepositorio
{
    Producto? ObtenerPorId(long id);
    Producto? ObtenerPorCodigo(string codigoBarras);

    /// <summary>Busca por parte del nombre o del código. Con texto vacío devuelve todos.</summary>
    IReadOnlyList<Producto> Buscar(string texto);

    void Agregar(Producto producto);
    void Actualizar(Producto producto);
    void Eliminar(long id);
    bool TieneVentas(long id);
}
