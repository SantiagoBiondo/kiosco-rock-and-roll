namespace KioscoRockAndRoll.Dominio;

/// <summary>
/// Se lanza cuando una operación viola una regla del negocio
/// (por ejemplo, un precio negativo o un código repetido).
/// El mensaje está pensado para mostrárselo directamente al usuario.
/// </summary>
public class ReglaDeNegocioException(string mensaje) : Exception(mensaje);
