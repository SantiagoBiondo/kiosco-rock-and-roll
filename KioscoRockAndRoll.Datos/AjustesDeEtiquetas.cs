namespace KioscoRockAndRoll.Datos;

/// <summary>Por qué impresora salen las hojas de etiquetas de precio (una común, con papel A4).</summary>
public sealed class AjustesDeEtiquetas(Ajustes ajustes)
{
    private const string ClaveImpresora = "Etiquetas.Impresora";

    /// <summary>La impresora elegida, o null si todavía no se eligió ninguna.</summary>
    public string? Impresora
    {
        get => ajustes.Leer(ClaveImpresora) is { Length: > 0 } nombre ? nombre : null;
        set => ajustes.Guardar(ClaveImpresora, value);
    }
}
