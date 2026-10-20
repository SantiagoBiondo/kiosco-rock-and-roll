using Microsoft.Data.Sqlite;

namespace KioscoRockAndRoll.Datos;

/// <summary>Sabe dónde está la base y abre conexiones a ella.</summary>
public sealed class ConexionSqlite
{
    private readonly string _cadenaConexion;

    public ConexionSqlite(string rutaArchivo)
    {
        RutaArchivo = rutaArchivo;
        _cadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = rutaArchivo,
            ForeignKeys = true,
            Pooling = false // así el archivo queda liberado al cerrar (útil para backups y tests)
        }.ToString();
    }

    public string RutaArchivo { get; }

    public SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        return conexion;
    }
}
