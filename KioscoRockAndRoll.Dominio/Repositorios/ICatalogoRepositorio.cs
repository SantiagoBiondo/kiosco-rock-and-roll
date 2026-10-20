using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Repositorios;

/// <summary>Dónde se guardan las categorías o los proveedores.</summary>
public interface ICatalogoRepositorio<T> where T : ElementoDeCatalogo
{
    /// <summary>Todos, ordenados por nombre.</summary>
    IReadOnlyList<T> Listar();

    T? ObtenerPorId(long id);
    void Agregar(T elemento);
    void Actualizar(T elemento);

    /// <summary>Lo borra. Los productos que lo tenían asignado quedan sin asignar.</summary>
    void Eliminar(long id);

    /// <summary>Cuántos productos lo tienen asignado.</summary>
    int ContarProductos(long id);
}
