namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Un renglón del carrito: un producto y cuántas unidades se llevan.</summary>
public class ItemCarrito
{
    internal ItemCarrito(Producto producto, int cantidad)
    {
        Producto = producto;
        Cantidad = cantidad;
    }

    public Producto Producto { get; internal set; }
    public int Cantidad { get; internal set; }
    public decimal PrecioUnitario => Producto.PrecioVenta;
    public decimal Subtotal => Cantidad * PrecioUnitario;

    /// <summary>True si se están llevando más unidades de las que figuran en stock.</summary>
    public bool SuperaElStock => !Producto.TieneStockPara(Cantidad);
}

/// <summary>
/// La venta que se está armando en el mostrador, antes de cobrarla.
/// </summary>
public class Carrito
{
    private readonly List<ItemCarrito> _items = [];

    public IReadOnlyList<ItemCarrito> Items => _items;
    public decimal Total => _items.Sum(i => i.Subtotal);
    public int Unidades => _items.Sum(i => i.Cantidad);
    public bool EstaVacio => _items.Count == 0;

    /// <summary>Se dispara cada vez que cambia el contenido del carrito.</summary>
    public event EventHandler? Cambio;

    /// <summary>
    /// Agrega unidades de un producto. Si el producto ya estaba, suma en el mismo renglón.
    /// </summary>
    public ItemCarrito Agregar(Producto producto, int cantidad = 1)
    {
        ArgumentNullException.ThrowIfNull(producto);
        if (producto.EsNuevo)
            throw new InvalidOperationException("Solo se pueden vender productos guardados.");
        if (cantidad <= 0)
            throw new ReglaDeNegocioException("La cantidad tiene que ser mayor a cero.");

        var item = Buscar(producto.Id);
        if (item == null)
        {
            item = new ItemCarrito(producto, cantidad);
            _items.Add(item);
        }
        else
        {
            item.Cantidad += cantidad;
        }

        AvisarCambio();
        return item;
    }

    /// <summary>Cambia la cantidad de un renglón. Con cantidad 0 o menos, lo quita.</summary>
    public void CambiarCantidad(long productoId, int cantidad)
    {
        var item = Buscar(productoId) ?? throw new InvalidOperationException("El producto no está en el carrito.");
        if (cantidad <= 0)
        {
            _items.Remove(item);
        }
        else
        {
            item.Cantidad = cantidad;
        }
        AvisarCambio();
    }

    public void Quitar(long productoId)
    {
        if (Buscar(productoId) is { } item && _items.Remove(item))
            AvisarCambio();
    }

    public void Vaciar()
    {
        if (EstaVacio)
            return;
        _items.Clear();
        AvisarCambio();
    }

    /// <summary>
    /// Reemplaza los datos de los productos (precio, stock) por versiones más nuevas.
    /// Los productos que ya no existen se quitan.
    /// </summary>
    public void Refrescar(Func<long, Producto?> obtenerProducto)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var actualizado = obtenerProducto(_items[i].Producto.Id);
            if (actualizado == null)
                _items.RemoveAt(i);
            else
                _items[i].Producto = actualizado;
        }
        AvisarCambio();
    }

    public ItemCarrito? Buscar(long productoId) => _items.FirstOrDefault(i => i.Producto.Id == productoId);

    private void AvisarCambio() => Cambio?.Invoke(this, EventArgs.Empty);
}
