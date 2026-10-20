namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Un producto del kiosco. Sus datos solo se pueden cambiar a través de
/// métodos que validan las reglas del negocio.
/// </summary>
public class Producto
{
    public long Id { get; private set; }
    public string CodigoBarras { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public decimal PrecioCosto { get; private set; }
    public decimal PrecioVenta { get; private set; }

    /// <summary>
    /// Puede quedar negativo: en un kiosco a veces se vende algo antes de registrar su ingreso,
    /// y es preferible no frenar la venta. El inventario lo muestra como stock bajo.
    /// </summary>
    public int Stock { get; private set; }

    public int StockMinimo { get; private set; }

    /// <summary>Categoría del producto, o null si no tiene.</summary>
    public long? CategoriaId { get; private set; }

    /// <summary>Proveedor habitual del producto, o null si no tiene.</summary>
    public long? ProveedorId { get; private set; }

    public Producto(string codigoBarras, string nombre, decimal precioCosto, decimal precioVenta,
        int stock = 0, int stockMinimo = 0)
    {
        ActualizarDatos(codigoBarras, nombre, precioCosto, precioVenta, stockMinimo);
        if (stock < 0)
            throw new ReglaDeNegocioException("El stock inicial no puede ser negativo.");
        Stock = stock;
    }

    /// <summary>Reconstruye un producto ya guardado. Lo usa la capa de datos.</summary>
    public static Producto Restaurar(long id, string codigoBarras, string nombre, decimal precioCosto,
        decimal precioVenta, int stock, int stockMinimo, long? categoriaId = null, long? proveedorId = null)
    {
        var producto = new Producto(codigoBarras, nombre, precioCosto, precioVenta, 0, stockMinimo) { Id = id };
        producto.Stock = stock;
        producto.Clasificar(categoriaId, proveedorId);
        return producto;
    }

    public bool EsNuevo => Id == 0;
    public bool StockBajo => Stock <= StockMinimo;
    public bool VendeAPerdida => PrecioVenta < PrecioCosto;

    public bool TieneStockPara(int cantidad) => Stock >= cantidad;

    public void AsignarId(long id)
    {
        if (!EsNuevo)
            throw new InvalidOperationException("El producto ya tiene un Id asignado.");
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
    }

    public void ActualizarDatos(string codigoBarras, string nombre, decimal precioCosto, decimal precioVenta, int stockMinimo)
    {
        codigoBarras = codigoBarras?.Trim() ?? "";
        nombre = nombre?.Trim() ?? "";

        if (codigoBarras == "")
            throw new ReglaDeNegocioException("El código de barras es obligatorio.");
        if (nombre == "")
            throw new ReglaDeNegocioException("El nombre es obligatorio.");
        if (precioCosto < 0 || precioVenta < 0)
            throw new ReglaDeNegocioException("Los precios no pueden ser negativos.");
        if (stockMinimo < 0)
            throw new ReglaDeNegocioException("El stock mínimo no puede ser negativo.");

        CodigoBarras = codigoBarras;
        Nombre = nombre;
        PrecioCosto = precioCosto;
        PrecioVenta = precioVenta;
        StockMinimo = stockMinimo;
    }

    /// <summary>Asigna categoría y proveedor (null = ninguno).</summary>
    public void Clasificar(long? categoriaId, long? proveedorId)
    {
        if (categoriaId <= 0 || proveedorId <= 0)
            throw new ArgumentOutOfRangeException(categoriaId <= 0 ? nameof(categoriaId) : nameof(proveedorId));
        CategoriaId = categoriaId;
        ProveedorId = proveedorId;
    }

    public void AjustarPrecios(AjustePrecios ajuste)
    {
        ArgumentNullException.ThrowIfNull(ajuste);
        PrecioCosto = ajuste.NuevoCosto(PrecioCosto);
        PrecioVenta = ajuste.NuevaVenta(PrecioVenta);
    }

    public void IngresarStock(int cantidad)
    {
        if (cantidad <= 0)
            throw new ReglaDeNegocioException("La cantidad a ingresar tiene que ser mayor a cero.");
        Stock += cantidad;
    }

    public void DescontarStock(int cantidad)
    {
        if (cantidad <= 0)
            throw new ReglaDeNegocioException("La cantidad a descontar tiene que ser mayor a cero.");
        Stock -= cantidad;
    }

    /// <summary>Corrige el stock a mano (por ejemplo, después de contar la mercadería).</summary>
    public void AjustarStock(int stockReal)
    {
        if (stockReal < 0)
            throw new ReglaDeNegocioException("El stock no puede ser negativo.");
        Stock = stockReal;
    }
}
