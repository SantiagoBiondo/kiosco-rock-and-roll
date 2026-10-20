using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class AnulacionTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 15, 0, 0);

    private static Turno TurnoAbierto(long id)
    {
        var turno = Turno.Abrir(0, Ahora);
        turno.AsignarId(id);
        return turno;
    }

    private static ResumenVenta Venta(long? turnoId, DateTime? anuladaEn = null) =>
        new(1, Ahora, MedioDePago.Efectivo, 2, 3000, turnoId, anuladaEn);

    [Fact]
    public void VentaDelTurnoAbierto_SePuedeAnular_YDevuelveElMotivoRecortado()
    {
        Assert.Equal("cobré de más", Venta(turnoId: 7).VerificarAnulacion(TurnoAbierto(7), "  cobré de más "));
    }

    [Fact]
    public void YaAnulada_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() =>
            Venta(turnoId: 7, anuladaEn: Ahora).VerificarAnulacion(TurnoAbierto(7), "x"));
    }

    [Fact]
    public void VentaDeOtroTurno_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Venta(turnoId: 6).VerificarAnulacion(TurnoAbierto(7), "x"));
    }

    [Fact]
    public void ConLaCajaCerrada_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => Venta(turnoId: 7).VerificarAnulacion(null, "x"));
    }

    [Fact]
    public void VentaSinTurno_Falla()
    {
        // Ventas de antes de que existieran los turnos.
        Assert.Throws<ReglaDeNegocioException>(() => Venta(turnoId: null).VerificarAnulacion(TurnoAbierto(7), "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SinMotivo_Falla(string motivo)
    {
        Assert.Throws<ReglaDeNegocioException>(() => Venta(turnoId: 7).VerificarAnulacion(TurnoAbierto(7), motivo));
    }

    [Fact]
    public void Estado_IndicaSiEstaAnulada()
    {
        Assert.Equal("", Venta(7).Estado);
        Assert.Equal("Anulada", Venta(7, Ahora).Estado);
    }
}
