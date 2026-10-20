namespace KioscoRockAndRoll.Datos;

/// <summary>Ajustes del programa guardados en la base, como pares clave/valor.</summary>
public sealed class Ajustes(ConexionSqlite conexion)
{
    public string? Leer(string clave)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = "SELECT Valor FROM Ajustes WHERE Clave = $clave";
        comando.Parameters.AddWithValue("$clave", clave);
        return comando.ExecuteScalar() as string;
    }

    /// <summary>Guarda el valor. Con null, borra el ajuste.</summary>
    public void Guardar(string clave, string? valor)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = valor is null
            ? "DELETE FROM Ajustes WHERE Clave = $clave"
            : """
              INSERT INTO Ajustes (Clave, Valor) VALUES ($clave, $valor)
              ON CONFLICT(Clave) DO UPDATE SET Valor = excluded.Valor
              """;
        comando.Parameters.AddWithValue("$clave", clave);
        if (valor is not null)
            comando.Parameters.AddWithValue("$valor", valor);
        comando.ExecuteNonQuery();
    }
}
