using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class CajaTests
{
    private static readonly DateTime Manana = new(2026, 10, 20, 8, 0, 0);

    private static Turno TurnoAbierto(decimal inicial = 5000)
    {
        var turno = Turno.Abrir(inicial, Manana);
        turno.AsignarId(1);
        return turno;
    }

    private static MovimientoCaja Movimiento(TipoMovimientoCaja tipo, decimal monto) =>
        MovimientoCaja.Crear(tipo, monto, "motivo", Manana);

    [Fact]
    public void Turno_AbreYCierra()
    {
        var turno = TurnoAbierto();
        Assert.True(turno.EstaAbierto);

        turno.Cerrar(7000, Manana.AddHours(8));

        Assert.False(turno.EstaAbierto);
        Assert.Equal(7000, turno.EfectivoContado);
    }

    [Fact]
    public void Turno_NoSeCierraDosVeces()
    {
        var turno = TurnoAbierto();
        turno.Cerrar(5000, Manana);
        Assert.Throws<ReglaDeNegocioException>(() => turno.Cerrar(5000, Manana));
    }

    [Fact]
    public void Turno_MontosNegativos_Fallan()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Turno.Abrir(-1, Manana));
        Assert.Throws<ReglaDeNegocioException>(() => TurnoAbierto().Cerrar(-1, Manana));
    }

    [Theory]
    [InlineData(0, "motivo")]
    [InlineData(-10, "motivo")]
    [InlineData(100, "  ")]
    public void Movimiento_Invalido_Falla(decimal monto, string motivo)
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            MovimientoCaja.Crear(TipoMovimientoCaja.Retiro, monto, motivo, Manana));
    }

    [Fact]
    public void Movimiento_EfectoSegunTipo()
    {
        Assert.Equal(300, Movimiento(TipoMovimientoCaja.Ingreso, 300).Efecto);
        Assert.Equal(-300, Movimiento(TipoMovimientoCaja.Retiro, 300).Efecto);
    }

    [Fact]
    public void Resumen_CalculaElEfectivoEsperado()
    {
        var resumen = new ResumenTurno(TurnoAbierto(inicial: 5000),
        [
            new TotalPorMedio(MedioDePago.Efectivo, 8, 12000),
            new TotalPorMedio(MedioDePago.Debito, 3, 4500),
            new TotalPorMedio(MedioDePago.TransferenciaQr, 2, 3000)
        ],
        [
            Movimiento(TipoMovimientoCaja.Retiro, 2000),
            Movimiento(TipoMovimientoCaja.Ingreso, 500)
        ],
        cantidadVentas: 12); // una de las ventas fue mixta: aparece en dos medios

        Assert.Equal(12000, resumen.VentasEnEfectivo);
        Assert.Equal(19500, resumen.TotalVendido);
        Assert.Equal(12, resumen.CantidadVentas);
        Assert.Equal(5000 + 12000 + 500 - 2000, resumen.EfectivoEsperado);
        Assert.Null(resumen.Diferencia); // sigue abierto
    }

    [Theory]
    [InlineData(5000, 0)]
    [InlineData(5100, 100)]
    [InlineData(4950, -50)]
    public void Resumen_DiferenciaAlCerrar(decimal contado, decimal diferencia)
    {
        var turno = TurnoAbierto(inicial: 5000);
        turno.Cerrar(contado, Manana);

        var resumen = new ResumenTurno(turno, [], [], cantidadVentas: 0);

        Assert.Equal(diferencia, resumen.Diferencia);
    }

    [Fact]
    public void DescribirDiferencia()
    {
        Assert.Equal("Exacto", ResumenTurno.DescribirDiferencia(0));
        Assert.StartsWith("Sobran", ResumenTurno.DescribirDiferencia(100));
        Assert.StartsWith("Faltan", ResumenTurno.DescribirDiferencia(-50));
    }

    [Fact]
    public void Venta_ConTurnoCerrado_Falla()
    {
        var turno = TurnoAbierto();
        turno.Cerrar(5000, Manana);
        var carrito = new Carrito();
        carrito.Agregar(Producto.Restaurar(1, "1", "Coca", 900, 1500, 10, 2));

        Assert.Throws<ReglaDeNegocioException>(() =>
            Venta.DesdeCarrito(carrito, Cobro.Crear(carrito.Total, MedioDePago.Efectivo), turno, Manana));
    }
}
