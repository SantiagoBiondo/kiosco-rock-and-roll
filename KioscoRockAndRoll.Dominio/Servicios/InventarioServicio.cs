using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;

namespace KioscoRockAndRoll.Dominio.Servicios;

/// <summary>Datos que se cargan en el formulario de alta/edición de un producto.</summary>
public sealed record DatosProducto(
    string CodigoBarras, string Nombre, decimal PrecioCosto, decimal PrecioVenta, int Stock, int StockMinimo,
    long? CategoriaId = null, long? ProveedorId = null);

/// <summary>Casos de uso del inventario: alta, edición, baja e ingreso de mercadería.</summary>
public class InventarioServicio(IProductoRepositorio productos)
{
    public Producto? ObtenerPorId(long id) => productos.ObtenerPorId(id);

    public Producto? ObtenerPorCodigo(string codigoBarras) => productos.ObtenerPorCodigo(codigoBarras.Trim());

    public IReadOnlyList<Producto> Buscar(string texto, bool soloStockBajo = false)
    {
        var resultado = productos.Buscar(texto ?? "");
        return soloStockBajo ? resultado.Where(p => p.StockBajo).ToList() : resultado;
    }

    public IReadOnlyList<Producto> Buscar(FiltroProductos filtro) =>
        productos.Buscar(filtro.Texto ?? "").Where(filtro.Acepta).ToList();

    /// <summary>Lo que hay que pedirle a un proveedor: sus productos con stock bajo.</summary>
    public PedidoAProveedor ArmarPedido(Proveedor proveedor) =>
        PedidoAProveedor.Armar(proveedor, Buscar(new FiltroProductos
        {
            SoloStockBajo = true,
            Proveedor = FiltroClasificacion.Solo(proveedor.Id)
        }));

    public Producto Crear(DatosProducto datos)
    {
        ValidarCodigoLibre(datos.CodigoBarras, idActual: null);

        var producto = new Producto(datos.CodigoBarras, datos.Nombre, datos.PrecioCosto, datos.PrecioVenta,
            datos.Stock, datos.StockMinimo);
        producto.Clasificar(datos.CategoriaId, datos.ProveedorId);
        productos.Agregar(producto);
        return producto;
    }

    public Producto Modificar(long id, DatosProducto datos)
    {
        var producto = productos.ObtenerPorId(id)
                       ?? throw new ReglaDeNegocioException("El producto ya no existe.");
        ValidarCodigoLibre(datos.CodigoBarras, idActual: id);

        producto.ActualizarDatos(datos.CodigoBarras, datos.Nombre, datos.PrecioCosto, datos.PrecioVenta, datos.StockMinimo);
        producto.AjustarStock(datos.Stock);
        producto.Clasificar(datos.CategoriaId, datos.ProveedorId);
        productos.Actualizar(producto);
        return producto;
    }

    /// <summary>
    /// Elimina un producto. Si ya tiene ventas no se puede borrar, porque se perdería el historial.
    /// </summary>
    public void Eliminar(long id)
    {
        if (productos.TieneVentas(id))
            throw new ReglaDeNegocioException(
                "Este producto ya tiene ventas registradas, así que no se puede eliminar.\n" +
                "Si no lo vendés más, dejale el stock en 0.");
        productos.Eliminar(id);
    }

    /// <summary>
    /// Suma stock a un producto existente. Devuelve null si el código no está registrado
    /// (en ese caso hay que darlo de alta).
    /// </summary>
    public Producto? IngresarMercaderia(string codigoBarras, int cantidad)
    {
        var producto = productos.ObtenerPorCodigo(codigoBarras.Trim());
        if (producto == null)
            return null;

        producto.IngresarStock(cantidad);
        productos.Actualizar(producto);
        return producto;
    }

    /// <summary>Calcula cómo quedarían los precios, sin modificar nada.</summary>
    public IReadOnlyList<CambioDePrecio> PrevisualizarAjuste(IEnumerable<Producto> aAjustar, AjustePrecios ajuste) =>
        aAjustar
            .Select(p => new CambioDePrecio(p.Id, p.Nombre,
                p.PrecioCosto, ajuste.NuevoCosto(p.PrecioCosto),
                p.PrecioVenta, ajuste.NuevaVenta(p.PrecioVenta)))
            .ToList();

    /// <summary>
    /// Aplica el ajuste a los productos indicados. Se guardan todos juntos:
    /// si algo falla, no queda ninguno a medio actualizar.
    /// </summary>
    /// <returns>Cuántos productos se actualizaron.</returns>
    public int AplicarAjuste(IReadOnlyCollection<long> productoIds, AjustePrecios ajuste)
    {
        ArgumentNullException.ThrowIfNull(ajuste);
        if (productoIds.Count == 0)
            throw new ReglaDeNegocioException("No hay productos para actualizar.");

        // Se leen de nuevo para trabajar con los datos más recientes.
        var aActualizar = productoIds
            .Distinct()
            .Select(productos.ObtenerPorId)
            .OfType<Producto>()
            .ToList();

        foreach (var producto in aActualizar)
            producto.AjustarPrecios(ajuste);

        productos.ActualizarVarios(aActualizar);
        return aActualizar.Count;
    }

    private void ValidarCodigoLibre(string codigoBarras, long? idActual)
    {
        var existente = productos.ObtenerPorCodigo(codigoBarras?.Trim() ?? "");
        if (existente != null && existente.Id != idActual)
            throw new ReglaDeNegocioException($"Ya existe un producto con ese código: {existente.Nombre}");
    }
}
