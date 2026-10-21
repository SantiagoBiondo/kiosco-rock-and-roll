using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Reportes;

namespace KioscoRockAndRoll.Tests.Dominio;

public class TicketTests
{
    private static readonly DateTime Fecha = new(2026, 10, 21, 18, 30, 0);

    private static Ticket Ticket(IReadOnlyList<Pago>? pagos = null, decimal? recibido = null, bool copia = false,
        string producto = "Coca-Cola 500 ml") => new(
        "Kiosco Rock & Roll", 12, Fecha,
        [new LineaTicket(2, producto, 1650), new LineaTicket(1, "Alfajor", 880)],
        pagos ?? [new Pago(MedioDePago.Efectivo, 4180)],
        recibido, copia);

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void NingunRenglonSePasaDelAncho(int columnas)
    {
        var ticket = Ticket(recibido: 5000, producto: "Alfajor triple de chocolate con dulce de leche y extra relleno");

        var renglones = new FormatoDeTicket(columnas).Renglones(ticket);

        Assert.All(renglones, r => Assert.True(r.Length <= columnas, $"\"{r}\" tiene {r.Length} caracteres"));
    }

    [Fact]
    public void LlevaLoNecesario()
    {
        var texto = string.Join("\n", new FormatoDeTicket(32).Renglones(Ticket(recibido: 5000)));

        Assert.Contains("Kiosco Rock & Roll", texto);
        Assert.Contains("No válido como factura", texto);
        Assert.Contains("Venta Nº 12", texto);
        Assert.Contains("21/10/2026 18:30", texto);
        Assert.Contains("Coca-Cola 500 ml", texto);
        Assert.Contains("2 x $ 1.650,00", texto);
        Assert.Contains("$ 3.300,00", texto);
        Assert.Contains("$ 4.180,00", texto);
        Assert.DoesNotContain("COPIA", texto);
    }

    [Fact]
    public void TotalYVuelto_AlineadosALaDerecha()
    {
        var renglones = new FormatoDeTicket(32).Renglones(Ticket(recibido: 5000));

        Assert.Contains("TOTAL                 $ 4.180,00", renglones);
        Assert.Contains("Paga con              $ 5.000,00", renglones);
        Assert.Contains("Vuelto                  $ 820,00", renglones);
    }

    [Fact]
    public void SinVuelto_NoMuestraPagaCon()
    {
        var texto = string.Join("\n", new FormatoDeTicket(32).Renglones(Ticket(recibido: null)));
        Assert.DoesNotContain("Vuelto", texto);
        Assert.DoesNotContain("Paga con", texto);
    }

    [Fact]
    public void PagoMixto_MuestraCadaMedio()
    {
        var ticket = Ticket([new Pago(MedioDePago.Efectivo, 3180), new Pago(MedioDePago.Debito, 1000)], recibido: 3500);

        var renglones = new FormatoDeTicket(32).Renglones(ticket);

        Assert.Contains(renglones, r => r.StartsWith("Efectivo") && r.EndsWith("$ 3.180,00"));
        Assert.Contains(renglones, r => r.StartsWith("Débito") && r.EndsWith("$ 1.000,00"));
        Assert.Contains(renglones, r => r.StartsWith("Vuelto") && r.EndsWith("$ 320,00"));
    }

    [Fact]
    public void NombreLargo_SePartePorPalabras()
    {
        var renglones = new FormatoDeTicket(32).Renglones(Ticket(producto: "Alfajor triple de chocolate con dulce de leche"));

        Assert.Contains("Alfajor triple de chocolate con", renglones);
        Assert.Contains("dulce de leche", renglones);
    }

    [Fact]
    public void Copia_LoDice()
    {
        var texto = string.Join("\n", new FormatoDeTicket(32).Renglones(Ticket(copia: true)));
        Assert.Contains("*** COPIA ***", texto);
    }

    [Fact]
    public void Vuelto_SoloSiEntregoMasQueLoQueDebia()
    {
        Assert.Equal(820, Ticket(recibido: 5000).Vuelto);
        Assert.Null(Ticket(recibido: 4180).Vuelto);
        Assert.Null(Ticket(recibido: null).Vuelto);
    }

    [Fact]
    public void CopiaDeVentaAnulada_Falla()
    {
        var anulada = new ResumenVenta(1, Fecha, MedioDePago.Efectivo, 1, 100, 1, AnuladaEn: Fecha, MotivoAnulacion: "x");
        var detalle = new DetalleVenta(anulada, [new LineaVenta(1, "Coca", 1, 100)]);

        Assert.Throws<ReglaDeNegocioException>(() => KioscoRockAndRoll.Dominio.Entidades.Ticket.CopiaDe(detalle, "Kiosco"));
    }

    [Fact]
    public void Configuracion_ColumnasSegunElPapel()
    {
        Assert.Equal(32, new ConfiguracionDeTicket("X", AnchoDePapel.Rollo58mm, false).Columnas);
        Assert.Equal(48, new ConfiguracionDeTicket("X", AnchoDePapel.Rollo80mm, false).Columnas);
        Assert.False(ConfiguracionDeTicket.PorDefecto.HayImpresora);
    }
}
