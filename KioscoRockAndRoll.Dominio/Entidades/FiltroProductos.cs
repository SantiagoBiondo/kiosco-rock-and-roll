namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Filtro por categoría o proveedor: todos, solo los que no tienen ninguno asignado, o uno en particular.
/// </summary>
public sealed record FiltroClasificacion
{
    private readonly bool _todos;

    private FiltroClasificacion(bool todos, long? id)
    {
        _todos = todos;
        Id = id;
    }

    public static FiltroClasificacion Todos { get; } = new(true, null);
    public static FiltroClasificacion SinAsignar { get; } = new(false, null);
    public static FiltroClasificacion Solo(long id) => new(false, id);

    /// <summary>El Id elegido, si el filtro es por uno en particular.</summary>
    public long? Id { get; }

    public bool Acepta(long? id) => _todos || id == Id;
}

/// <summary>Criterios para listar productos en el inventario.</summary>
public sealed record FiltroProductos
{
    public string Texto { get; init; } = "";
    public bool SoloStockBajo { get; init; }
    public FiltroClasificacion Categoria { get; init; } = FiltroClasificacion.Todos;
    public FiltroClasificacion Proveedor { get; init; } = FiltroClasificacion.Todos;

    public bool Acepta(Producto producto) =>
        (!SoloStockBajo || producto.StockBajo)
        && Categoria.Acepta(producto.CategoriaId)
        && Proveedor.Acepta(producto.ProveedorId);
}
