namespace KioscoRockAndRoll.Datos;

/// <summary>Cómo se ve el programa (por ahora, el tema claro u oscuro).</summary>
public sealed class AjustesDeApariencia(Ajustes ajustes)
{
    private const string ClaveTemaOscuro = "Apariencia.TemaOscuro";

    public bool TemaOscuro
    {
        get => bool.TryParse(ajustes.Leer(ClaveTemaOscuro), out var oscuro) && oscuro;
        set => ajustes.Guardar(ClaveTemaOscuro, value.ToString());
    }
}
