namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Algo con nombre que sirve para agrupar productos (una categoría, un proveedor).
/// El nombre es obligatorio y no se puede repetir (eso último lo controla el servicio).
/// </summary>
public abstract class ElementoDeCatalogo
{
    public const int LargoMaximoNombre = 60;

    protected ElementoDeCatalogo(string nombre) => Renombrar(nombre);

    public long Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public bool EsNuevo => Id == 0;

    public void AsignarId(long id)
    {
        if (!EsNuevo)
            throw new InvalidOperationException("Ya tiene un Id asignado.");
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
    }

    public void Renombrar(string nombre)
    {
        nombre = nombre?.Trim() ?? "";
        if (nombre == "")
            throw new ReglaDeNegocioException("El nombre es obligatorio.");
        if (nombre.Length > LargoMaximoNombre)
            throw new ReglaDeNegocioException($"El nombre no puede tener más de {LargoMaximoNombre} letras.");
        Nombre = nombre;
    }

    public override string ToString() => Nombre;
}

public sealed class Categoria(string nombre) : ElementoDeCatalogo(nombre)
{
    public static Categoria Restaurar(long id, string nombre)
    {
        var categoria = new Categoria(nombre);
        categoria.AsignarId(id);
        return categoria;
    }
}

public sealed class Proveedor : ElementoDeCatalogo
{
    public Proveedor(string nombre, string? telefono = null) : base(nombre) => CambiarTelefono(telefono);

    public string Telefono { get; private set; } = "";

    public void CambiarTelefono(string? telefono) => Telefono = telefono?.Trim() ?? "";

    public static Proveedor Restaurar(long id, string nombre, string telefono)
    {
        var proveedor = new Proveedor(nombre, telefono);
        proveedor.AsignarId(id);
        return proveedor;
    }
}
