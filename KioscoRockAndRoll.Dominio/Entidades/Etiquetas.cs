namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Una etiqueta de precio para la góndola: el nombre del producto y su precio de venta.</summary>
public sealed record Etiqueta(string Nombre, decimal Precio);

/// <summary>Un renglón de la lista: un producto y cuántas etiquetas suyas imprimir.</summary>
public sealed class EtiquetaPedida
{
    internal EtiquetaPedida(Producto producto, int cantidad)
    {
        Producto = producto;
        Cantidad = cantidad;
    }

    public Producto Producto { get; internal set; }
    public int Cantidad { get; internal set; }
    public string Nombre => Producto.Nombre;
    public decimal Precio => Producto.PrecioVenta;
}

/// <summary>
/// Los productos a los que se les van a imprimir etiquetas de precio, en el orden en que se escanearon.
/// Escanear dos veces el mismo producto suma una etiqueta más en el mismo renglón.
/// </summary>
public class ListaDeEtiquetas
{
    private readonly List<EtiquetaPedida> _pedidas = [];

    public IReadOnlyList<EtiquetaPedida> Pedidas => _pedidas;
    public int Total => _pedidas.Sum(p => p.Cantidad);
    public bool EstaVacia => _pedidas.Count == 0;

    /// <summary>Se dispara cada vez que cambia la lista.</summary>
    public event EventHandler? Cambio;

    public EtiquetaPedida Agregar(Producto producto, int cantidad = 1)
    {
        ArgumentNullException.ThrowIfNull(producto);
        if (producto.EsNuevo)
            throw new InvalidOperationException("Solo se pueden agregar productos guardados.");
        if (cantidad <= 0)
            throw new ReglaDeNegocioException("La cantidad de etiquetas tiene que ser mayor a cero.");

        var pedida = Buscar(producto.Id);
        if (pedida == null)
        {
            pedida = new EtiquetaPedida(producto, cantidad);
            _pedidas.Add(pedida);
        }
        else
        {
            pedida.Cantidad += cantidad;
        }

        AvisarCambio();
        return pedida;
    }

    /// <summary>Cambia cuántas etiquetas se imprimen de un producto. Con 0 o menos, lo quita.</summary>
    public void CambiarCantidad(long productoId, int cantidad)
    {
        var pedida = Buscar(productoId) ?? throw new InvalidOperationException("El producto no está en la lista.");
        if (cantidad <= 0)
            _pedidas.Remove(pedida);
        else
            pedida.Cantidad = cantidad;
        AvisarCambio();
    }

    public void Quitar(long productoId)
    {
        if (Buscar(productoId) is { } pedida && _pedidas.Remove(pedida))
            AvisarCambio();
    }

    public void Vaciar()
    {
        if (EstaVacia)
            return;
        _pedidas.Clear();
        AvisarCambio();
    }

    /// <summary>
    /// Vuelve a leer los productos, por si cambiaron los precios (por ejemplo, después de un aumento).
    /// Los que ya no existen se quitan.
    /// </summary>
    public void Refrescar(Func<long, Producto?> obtenerProducto)
    {
        for (var i = _pedidas.Count - 1; i >= 0; i--)
        {
            var actualizado = obtenerProducto(_pedidas[i].Producto.Id);
            if (actualizado == null)
                _pedidas.RemoveAt(i);
            else
                _pedidas[i].Producto = actualizado;
        }
        AvisarCambio();
    }

    /// <summary>Todas las etiquetas a imprimir, una por una, en el orden de la lista.</summary>
    public IReadOnlyList<Etiqueta> Etiquetas() =>
        _pedidas.SelectMany(p => Enumerable.Repeat(new Etiqueta(p.Nombre, p.Precio), p.Cantidad)).ToList();

    /// <summary>Cuántas hojas hacen falta si en cada una entran <paramref name="porHoja"/> etiquetas.</summary>
    public int Hojas(int porHoja)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(porHoja);
        return (Total + porHoja - 1) / porHoja;
    }

    public EtiquetaPedida? Buscar(long productoId) => _pedidas.FirstOrDefault(p => p.Producto.Id == productoId);

    private void AvisarCambio() => Cambio?.Invoke(this, EventArgs.Empty);
}
