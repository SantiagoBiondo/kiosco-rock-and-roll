using System.Globalization;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Reportes;

/// <summary>
/// Arma el ticket como renglones de texto de un ancho fijo (32 caracteres en un rollo de 58 mm,
/// 48 en uno de 80 mm). Se imprime con una letra de ancho fijo, así las columnas quedan alineadas.
/// </summary>
public sealed class FormatoDeTicket
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private readonly int _columnas;

    public FormatoDeTicket(int columnas)
    {
        if (columnas < 24)
            throw new ArgumentOutOfRangeException(nameof(columnas), "Un ticket necesita al menos 24 columnas.");
        _columnas = columnas;
    }

    public IReadOnlyList<string> Renglones(Ticket ticket)
    {
        var renglones = new List<string>();
        var separador = new string('-', _columnas);

        renglones.AddRange(Centrado(ticket.Negocio));
        renglones.AddRange(Centrado("No válido como factura"));
        renglones.Add(separador);
        renglones.Add(IzquierdaDerecha($"Venta Nº {ticket.NumeroVenta}", ticket.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura)));
        if (ticket.EsCopia)
            renglones.AddRange(Centrado("*** COPIA ***"));
        renglones.Add(separador);

        foreach (var linea in ticket.Lineas)
        {
            // Primero la descripción (en varios renglones si es larga), después cantidad × precio e importe.
            renglones.AddRange(Partir(linea.Descripcion));
            renglones.Add(IzquierdaDerecha($"  {linea.Cantidad} x {Plata(linea.PrecioUnitario)}", Plata(linea.Importe)));
        }

        renglones.Add(separador);
        renglones.Add(IzquierdaDerecha("TOTAL", Plata(ticket.Total)));
        foreach (var pago in ticket.Pagos)
            renglones.Add(IzquierdaDerecha(pago.Medio.Descripcion(), Plata(pago.Monto)));
        if (ticket.Vuelto is { } vuelto)
        {
            renglones.Add(IzquierdaDerecha("Paga con", Plata(ticket.EfectivoRecibido!.Value)));
            renglones.Add(IzquierdaDerecha("Vuelto", Plata(vuelto)));
        }
        renglones.Add(separador);
        renglones.AddRange(Centrado("¡Gracias por su compra!"));
        return renglones;
    }

    private static string Plata(decimal monto) => monto.ToString("C2", Cultura);

    /// <summary>Texto a la izquierda y a la derecha del mismo renglón; si no entran, se acorta el de la izquierda.</summary>
    private string IzquierdaDerecha(string izquierda, string derecha)
    {
        var espacioIzquierda = Math.Max(_columnas - derecha.Length - 1, 0);
        if (izquierda.Length > espacioIzquierda)
            izquierda = izquierda[..espacioIzquierda];
        return izquierda.PadRight(_columnas - derecha.Length) + derecha;
    }

    private IEnumerable<string> Centrado(string texto) =>
        Partir(texto).Select(r => r.PadLeft((_columnas + r.Length) / 2).TrimEnd());

    /// <summary>Corta un texto largo en renglones, sin partir palabras si se puede.</summary>
    private IEnumerable<string> Partir(string texto)
    {
        var actual = "";
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Una palabra más larga que el renglón se corta a la fuerza.
            var resto = palabra;
            while (resto.Length > _columnas)
            {
                if (actual != "")
                {
                    yield return actual;
                    actual = "";
                }
                yield return resto[.._columnas];
                resto = resto[_columnas..];
            }

            if (actual == "")
                actual = resto;
            else if (actual.Length + 1 + resto.Length <= _columnas)
                actual += " " + resto;
            else
            {
                yield return actual;
                actual = resto;
            }
        }
        if (actual != "")
            yield return actual;
    }
}
