using System.Text;

namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Un renglón del pedido. La cantidad se puede corregir antes de enviarlo.</summary>
public sealed class LineaPedido
{
    private int _cantidad;

    internal LineaPedido(Producto producto, int cantidad)
    {
        ProductoId = producto.Id;
        Nombre = producto.Nombre;
        Stock = producto.Stock;
        StockMinimo = producto.StockMinimo;
        Cantidad = cantidad;
    }

    public long ProductoId { get; }
    public string Nombre { get; }
    public int Stock { get; }
    public int StockMinimo { get; }

    /// <summary>Cuántas unidades pedir. En 0, el renglón no se incluye en el texto del pedido.</summary>
    public int Cantidad
    {
        get => _cantidad;
        set
        {
            if (value < 0)
                throw new ReglaDeNegocioException("La cantidad a pedir no puede ser negativa.");
            _cantidad = value;
        }
    }
}

/// <summary>
/// Lista de lo que hay que pedirle a un proveedor: sus productos con stock bajo.
/// </summary>
public sealed class PedidoAProveedor
{
    private PedidoAProveedor(Proveedor proveedor, IReadOnlyList<LineaPedido> lineas)
    {
        Proveedor = proveedor;
        Lineas = lineas;
    }

    public Proveedor Proveedor { get; }
    public IReadOnlyList<LineaPedido> Lineas { get; }
    public bool EstaVacio => Lineas.All(l => l.Cantidad == 0);

    /// <summary>
    /// Arma el pedido con los productos del proveedor que tienen stock bajo.
    /// Sugiere reponer hasta el doble del stock mínimo (al menos 1 unidad).
    /// </summary>
    public static PedidoAProveedor Armar(Proveedor proveedor, IEnumerable<Producto> productos)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        var lineas = productos
            .Where(p => p.ProveedorId == proveedor.Id && p.StockBajo)
            .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new LineaPedido(p, CantidadSugerida(p)))
            .ToList();
        return new PedidoAProveedor(proveedor, lineas);
    }

    public static int CantidadSugerida(Producto producto) =>
        Math.Max(producto.StockMinimo * 2 - producto.Stock, 1);

    /// <summary>Texto listo para mandar por WhatsApp o mail.</summary>
    public string ATexto(string nombreNegocio, DateOnly fecha)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"Pedido de {nombreNegocio} - {fecha:dd/MM/yyyy}");
        texto.AppendLine($"Proveedor: {Proveedor.Nombre}");
        texto.AppendLine();
        foreach (var linea in Lineas.Where(l => l.Cantidad > 0))
            texto.AppendLine($"- {linea.Cantidad} x {linea.Nombre}");
        return texto.ToString().TrimEnd();
    }
}
