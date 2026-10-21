using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Datos;

/// <summary>Guarda y lee la configuración de la impresora de tickets en la tabla de ajustes.</summary>
public sealed class AjustesDeTicket(Ajustes ajustes)
{
    private const string ClaveImpresora = "Ticket.Impresora";
    private const string ClaveAncho = "Ticket.AnchoPapelMm";
    private const string ClaveImprimirAlCobrar = "Ticket.ImprimirAlCobrar";

    public ConfiguracionDeTicket Leer()
    {
        var porDefecto = ConfiguracionDeTicket.PorDefecto;
        var ancho = int.TryParse(ajustes.Leer(ClaveAncho), out var mm) && Enum.IsDefined((AnchoDePapel)mm)
            ? (AnchoDePapel)mm
            : porDefecto.Ancho;
        var imprimir = bool.TryParse(ajustes.Leer(ClaveImprimirAlCobrar), out var valor) ? valor : porDefecto.ImprimirAlCobrar;
        return new ConfiguracionDeTicket(ajustes.Leer(ClaveImpresora), ancho, imprimir);
    }

    public void Guardar(ConfiguracionDeTicket configuracion)
    {
        ajustes.Guardar(ClaveImpresora, configuracion.HayImpresora ? configuracion.Impresora : null);
        ajustes.Guardar(ClaveAncho, ((int)configuracion.Ancho).ToString());
        ajustes.Guardar(ClaveImprimirAlCobrar, configuracion.ImprimirAlCobrar.ToString());
    }
}
