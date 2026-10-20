using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Dominio.Repositorios;

public interface IVentaRepositorio
{
    /// <summary>
    /// Guarda la venta y descuenta el stock de cada producto en una sola operación atómica:
    /// o se guarda todo, o no se guarda nada.
    /// </summary>
    void Registrar(Venta venta);

    IReadOnlyList<ResumenVenta> ListarDelDia(DateOnly dia);
}
