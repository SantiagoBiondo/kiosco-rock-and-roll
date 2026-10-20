using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Alta, modificación y baja de categorías o proveedores.</summary>
public class CatalogoServicio<T>(ICatalogoRepositorio<T> repositorio) where T : ElementoDeCatalogo
{
    public IReadOnlyList<T> Listar() => repositorio.Listar();

    public T? ObtenerPorId(long id) => repositorio.ObtenerPorId(id);

    /// <summary>Guarda un elemento nuevo o modificado. No permite nombres repetidos.</summary>
    public void Guardar(T elemento)
    {
        ArgumentNullException.ThrowIfNull(elemento);

        var repetido = repositorio.Listar().FirstOrDefault(e =>
            e.Id != elemento.Id && string.Equals(e.Nombre, elemento.Nombre, StringComparison.CurrentCultureIgnoreCase));
        if (repetido != null)
            throw new ReglaDeNegocioException($"Ya existe \"{repetido.Nombre}\".");

        if (elemento.EsNuevo)
            repositorio.Agregar(elemento);
        else
            repositorio.Actualizar(elemento);
    }

    public int ContarProductos(long id) => repositorio.ContarProductos(id);

    /// <summary>Lo elimina. Sus productos no se borran: quedan sin asignar.</summary>
    public void Eliminar(long id) => repositorio.Eliminar(id);
}
