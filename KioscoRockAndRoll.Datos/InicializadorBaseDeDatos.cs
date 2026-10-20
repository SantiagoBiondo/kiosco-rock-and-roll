namespace KioscoRockAndRoll.Datos;

/// <summary>
/// Crea la base si no existe y la pone al día aplicando las migraciones que le falten.
/// La versión del esquema se guarda en la base misma (PRAGMA user_version), así una base
/// vieja se actualiza sola al abrir el programa, sin perder datos.
/// </summary>
public sealed class InicializadorBaseDeDatos(ConexionSqlite conexion)
{
    /// <summary>
    /// Cada migración lleva la base de la versión N-1 a la N. Nunca se modifica una que ya
    /// se usó: los cambios nuevos van en una migración nueva al final.
    /// </summary>
    internal static readonly IReadOnlyList<string> Migraciones =
    [
        // 1: esquema inicial. Los montos se guardan en centavos (enteros) para no tener errores de redondeo.
        """
        CREATE TABLE IF NOT EXISTS Productos (
            Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
            CodigoBarras        TEXT    NOT NULL UNIQUE,
            Nombre              TEXT    NOT NULL,
            PrecioCostoCentavos INTEGER NOT NULL,
            PrecioVentaCentavos INTEGER NOT NULL,
            Stock               INTEGER NOT NULL,
            StockMinimo         INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Ventas (
            Id        INTEGER PRIMARY KEY AUTOINCREMENT,
            Fecha     TEXT    NOT NULL,
            MedioPago INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS VentaLineas (
            Id                     INTEGER PRIMARY KEY AUTOINCREMENT,
            VentaId                INTEGER NOT NULL REFERENCES Ventas(Id),
            ProductoId             INTEGER NOT NULL REFERENCES Productos(Id),
            NombreProducto         TEXT    NOT NULL,
            Cantidad               INTEGER NOT NULL,
            PrecioUnitarioCentavos INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Ventas_Fecha ON Ventas(Fecha);
        CREATE INDEX IF NOT EXISTS IX_VentaLineas_VentaId ON VentaLineas(VentaId);
        CREATE INDEX IF NOT EXISTS IX_VentaLineas_ProductoId ON VentaLineas(ProductoId);
        """,

        // 2: categorías y proveedores. Al borrar uno, sus productos quedan sin asignar (no se borran).
        """
        CREATE TABLE Categorias (
            Id     INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre TEXT    NOT NULL UNIQUE COLLATE NOCASE
        );

        CREATE TABLE Proveedores (
            Id       INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre   TEXT    NOT NULL UNIQUE COLLATE NOCASE,
            Telefono TEXT    NOT NULL DEFAULT ''
        );

        ALTER TABLE Productos ADD COLUMN CategoriaId INTEGER REFERENCES Categorias(Id) ON DELETE SET NULL;
        ALTER TABLE Productos ADD COLUMN ProveedorId INTEGER REFERENCES Proveedores(Id) ON DELETE SET NULL;

        CREATE INDEX IX_Productos_CategoriaId ON Productos(CategoriaId);
        CREATE INDEX IX_Productos_ProveedorId ON Productos(ProveedorId);
        """
    ];

    public static int VersionActual => Migraciones.Count;

    public void Inicializar()
    {
        var carpeta = Path.GetDirectoryName(conexion.RutaArchivo);
        if (!string.IsNullOrEmpty(carpeta))
            Directory.CreateDirectory(carpeta);

        using var db = conexion.Abrir();
        var version = LeerVersion(db);
        if (version > VersionActual)
            throw new InvalidOperationException(
                "La base de datos es de una versión más nueva del programa. Actualizá el programa para abrirla.");

        // Cada migración va en su propia transacción junto con el cambio de versión:
        // si falla, la base queda como estaba.
        for (var siguiente = version + 1; siguiente <= VersionActual; siguiente++)
        {
            using var transaccion = db.BeginTransaction();
            using var comando = db.CreateCommand();
            comando.CommandText = $"{Migraciones[siguiente - 1]};\nPRAGMA user_version = {siguiente};";
            comando.ExecuteNonQuery();
            transaccion.Commit();
        }
    }

    internal static int LeerVersion(Microsoft.Data.Sqlite.SqliteConnection db)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(comando.ExecuteScalar());
    }
}
