using System.Globalization;

namespace KioscoRockAndRoll.Datos;

/// <summary>Conversiones entre los tipos de C# y cómo se guardan en SQLite.</summary>
internal static class Conversiones
{
    public const string FormatoFecha = "yyyy-MM-dd HH:mm:ss";

    public static long ACentavos(decimal monto) => (long)decimal.Round(monto * 100, MidpointRounding.AwayFromZero);

    public static decimal DeCentavos(long centavos) => centavos / 100m;

    public static string AFechaTexto(DateTime fecha) => fecha.ToString(FormatoFecha, CultureInfo.InvariantCulture);

    public static DateTime DeFechaTexto(string texto) =>
        DateTime.ParseExact(texto, FormatoFecha, CultureInfo.InvariantCulture);
}
