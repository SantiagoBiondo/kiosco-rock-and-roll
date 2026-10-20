namespace KioscoRockAndRoll.Datos;

/// <summary>Crea el archivo de la base y las tablas si todavía no existen.</summary>
public sealed class InicializadorBaseDeDatos(ConexionSqlite conexion)
{
    public void Inicializar()
    {
        var carpeta = Path.GetDirectoryName(conexion.RutaArchivo);
        if (!string.IsNullOrEmpty(carpeta))
            Directory.CreateDirectory(carpeta);

        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();

        // Los montos se guardan en centavos (enteros) para no tener errores de redondeo.
        comando.CommandText = """
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
            """;
        comando.ExecuteNonQuery();
    }
}
