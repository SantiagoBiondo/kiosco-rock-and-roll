namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Lo que se ingresó en el campo de código: lo que manda el lector de barras,
/// o "cantidad*código" escrito a mano (por ejemplo "3*7790895000997").
/// </summary>
public sealed record CodigoEscaneado(string Codigo, int Cantidad)
{
    public static bool TryInterpretar(string? texto, out CodigoEscaneado resultado)
    {
        resultado = new CodigoEscaneado("", 0);
        texto = texto?.Trim() ?? "";
        if (texto == "")
            return false;

        var partes = texto.Split('*', 2, StringSplitOptions.TrimEntries);
        if (partes.Length == 2)
        {
            if (!int.TryParse(partes[0], out var cantidad) || cantidad <= 0 || partes[1] == "")
                return false;
            resultado = new CodigoEscaneado(partes[1], cantidad);
            return true;
        }

        resultado = new CodigoEscaneado(texto, 1);
        return true;
    }
}
