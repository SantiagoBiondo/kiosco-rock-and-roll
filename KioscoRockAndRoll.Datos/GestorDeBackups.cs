using Microsoft.Data.Sqlite;

namespace KioscoRockAndRoll.Datos;

/// <summary>Un archivo de backup.</summary>
public sealed record InfoBackup(string Ruta, DateTime Fecha, long Bytes)
{
    public string Nombre => Path.GetFileName(Ruta);
    public string Tamanio => Bytes < 1024 * 1024 ? $"{Bytes / 1024.0:0.#} KB" : $"{Bytes / (1024.0 * 1024):0.#} MB";
}

/// <summary>Cómo le fue al backup en una carpeta.</summary>
public sealed record ResultadoDestino(string Carpeta, bool Exito, string? Error = null);

public sealed record ResultadoBackup(IReadOnlyList<ResultadoDestino> Destinos)
{
    public bool TodoBien => Destinos.All(d => d.Exito);
}

/// <summary>
/// Copias de seguridad de la base. Hay una copia por día (si se hace varias veces el mismo día,
/// se actualiza) y se conservan las últimas <see cref="CantidadAConservar"/>. Además de la carpeta
/// principal, se puede configurar una carpeta extra (un pendrive o una carpeta de Google Drive).
/// </summary>
public sealed class GestorDeBackups(ConexionSqlite conexion, Ajustes ajustes, TimeProvider reloj)
{
    public const int CantidadAConservar = 30;

    private const string Prefijo = "kiosco_";
    private const string ClaveCarpetaExtra = "Backup.CarpetaExtra";

    /// <summary>Carpeta "backups" al lado del archivo de la base.</summary>
    public string CarpetaPrincipal =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(conexion.RutaArchivo))!, "backups");

    /// <summary>Segunda carpeta donde copiar los backups, o null si no hay.</summary>
    public string? CarpetaExtra
    {
        get => ajustes.Leer(ClaveCarpetaExtra);
        set
        {
            if (value is not null && !Directory.Exists(value))
                throw new DirectoryNotFoundException($"La carpeta \"{value}\" no existe.");
            ajustes.Guardar(ClaveCarpetaExtra, value);
        }
    }

    private DateTime Ahora => reloj.GetLocalNow().DateTime;

    /// <summary>Hace (o actualiza) el backup del día en la carpeta principal y en la extra.</summary>
    public ResultadoBackup HacerBackup()
    {
        var nombre = $"{Prefijo}{Ahora:yyyy-MM-dd}.db";
        var resultados = new List<ResultadoDestino> { CopiarA(CarpetaPrincipal, nombre, crearCarpeta: true) };

        // La carpeta extra no se crea: si no existe, probablemente el pendrive no está conectado.
        if (CarpetaExtra is { } extra)
            resultados.Add(CopiarA(extra, nombre, crearCarpeta: false));

        return new ResultadoBackup(resultados);
    }

    /// <summary>
    /// Copia puntual antes de una operación delicada (migrar, restaurar). Solo en la carpeta principal.
    /// Si falla, lanza una excepción para que la operación no siga sin respaldo.
    /// </summary>
    /// <returns>La ruta del archivo creado.</returns>
    public string HacerCopiaEspecial(string motivo)
    {
        var nombre = $"{Prefijo}{Ahora:yyyy-MM-dd_HHmmss}_{motivo}.db";
        var resultado = CopiarA(CarpetaPrincipal, nombre, crearCarpeta: true);
        if (!resultado.Exito)
            throw new IOException($"No se pudo hacer la copia de seguridad: {resultado.Error}");
        return Path.Combine(CarpetaPrincipal, nombre);
    }

    /// <summary>Los backups de la carpeta principal, del más nuevo al más viejo.</summary>
    public IReadOnlyList<InfoBackup> Listar()
    {
        if (!Directory.Exists(CarpetaPrincipal))
            return [];

        return Directory.GetFiles(CarpetaPrincipal, $"{Prefijo}*.db")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Select(ruta => new FileInfo(ruta))
            .Select(info => new InfoBackup(info.FullName, info.LastWriteTime, info.Length))
            .ToList();
    }

    /// <summary>
    /// Reemplaza la base actual por el contenido de un backup. Antes guarda una copia del
    /// estado actual, por si hubo un error. Después hay que reiniciar el programa.
    /// </summary>
    public void Restaurar(string rutaBackup)
    {
        Verificar(rutaBackup);
        HacerCopiaEspecial("antes-de-restaurar");

        using var origen = AbrirArchivo(rutaBackup, soloLectura: true);
        using var destino = conexion.Abrir();
        origen.BackupDatabase(destino);
    }

    private ResultadoDestino CopiarA(string carpeta, string nombre, bool crearCarpeta)
    {
        var final = Path.Combine(carpeta, nombre);
        var temporal = final + ".tmp";
        try
        {
            if (crearCarpeta)
                Directory.CreateDirectory(carpeta);
            else if (!Directory.Exists(carpeta))
                return new ResultadoDestino(carpeta, false, "La carpeta no está disponible (¿está conectado el pendrive?).");

            // La API de backup de SQLite hace una copia consistente aunque la base esté en uso.
            // Se escribe en un temporal y recién al final se reemplaza el archivo, así un corte
            // a mitad de camino no arruina el backup anterior.
            using (var origen = conexion.Abrir())
            using (var destino = AbrirArchivo(temporal, soloLectura: false))
                origen.BackupDatabase(destino);

            Verificar(temporal);
            File.Move(temporal, final, overwrite: true);
            AplicarRetencion(carpeta);
            return new ResultadoDestino(carpeta, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or InvalidDataException)
        {
            try { File.Delete(temporal); } catch (IOException) { }
            return new ResultadoDestino(carpeta, false, ex.Message);
        }
    }

    /// <summary>Borra los backups más viejos. Solo toca archivos creados por este programa.</summary>
    private static void AplicarRetencion(string carpeta)
    {
        var sobrantes = Directory.GetFiles(carpeta, $"{Prefijo}*.db")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(CantidadAConservar);
        foreach (var archivo in sobrantes)
            File.Delete(archivo);
    }

    /// <summary>Comprueba que el archivo sea una base del kiosco y que no esté dañada.</summary>
    private static void Verificar(string ruta)
    {
        if (!File.Exists(ruta))
            throw new InvalidDataException("El archivo de backup no existe.");

        using var db = AbrirArchivo(ruta, soloLectura: true);
        using var comando = db.CreateCommand();
        comando.CommandText = "PRAGMA quick_check";
        if (comando.ExecuteScalar() as string != "ok")
            throw new InvalidDataException("El archivo de backup está dañado.");

        comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Productos'";
        if (Convert.ToInt32(comando.ExecuteScalar()) != 1)
            throw new InvalidDataException("El archivo no es un backup del kiosco.");
    }

    private static SqliteConnection AbrirArchivo(string ruta, bool soloLectura)
    {
        var conexionArchivo = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = ruta,
            Mode = soloLectura ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        conexionArchivo.Open();
        return conexionArchivo;
    }
}
