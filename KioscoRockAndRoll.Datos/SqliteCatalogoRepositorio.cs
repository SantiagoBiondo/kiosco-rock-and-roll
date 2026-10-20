using KioscoRockAndRoll.Dominio.Entidades;
using KioscoRockAndRoll.Dominio.Repositorios;
using Microsoft.Data.Sqlite;

namespace KioscoRockAndRoll.Datos;

/// <summary>
/// Lo común a las tablas de catálogo (Categorias, Proveedores). Cada subclase dice
/// su tabla, sus columnas extra y cómo leer y guardar sus datos.
/// </summary>
public abstract class SqliteCatalogoRepositorio<T>(ConexionSqlite conexion) : ICatalogoRepositorio<T>
    where T : ElementoDeCatalogo
{
    /// <summary>Nombre de la tabla.</summary>
    protected abstract string Tabla { get; }

    /// <summary>Columna de Productos que apunta a esta tabla.</summary>
    protected abstract string ColumnaEnProductos { get; }

    /// <summary>Columnas además de Id y Nombre, en el orden en que las lee <see cref="Leer"/>.</summary>
    protected virtual string[] ColumnasExtra => [];

    /// <summary>Arma el objeto desde una fila: columna 0 = Id, 1 = Nombre, 2 en adelante = las extra.</summary>
    protected abstract T Leer(SqliteDataReader lector);

    /// <summary>Carga los parámetros de las columnas extra ($columna).</summary>
    protected virtual void CargarParametrosExtra(SqliteCommand comando, T elemento) { }

    private string Columnas => string.Join(", ", ["Id", "Nombre", .. ColumnasExtra]);

    public IReadOnlyList<T> Listar()
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"SELECT {Columnas} FROM {Tabla} ORDER BY Nombre COLLATE NOCASE";

        var lista = new List<T>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            lista.Add(Leer(lector));
        return lista;
    }

    public T? ObtenerPorId(long id)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"SELECT {Columnas} FROM {Tabla} WHERE Id = $id";
        comando.Parameters.AddWithValue("$id", id);

        using var lector = comando.ExecuteReader();
        return lector.Read() ? Leer(lector) : null;
    }

    public void Agregar(T elemento)
    {
        string[] columnas = ["Nombre", .. ColumnasExtra];
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"""
            INSERT INTO {Tabla} ({string.Join(", ", columnas)})
            VALUES ({string.Join(", ", columnas.Select(c => "$" + c))})
            RETURNING Id
            """;
        CargarParametros(comando, elemento);
        elemento.AsignarId((long)comando.ExecuteScalar()!);
    }

    public void Actualizar(T elemento)
    {
        string[] columnas = ["Nombre", .. ColumnasExtra];
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"""
            UPDATE {Tabla} SET {string.Join(", ", columnas.Select(c => $"{c} = ${c}"))}
            WHERE Id = $id
            """;
        CargarParametros(comando, elemento);
        comando.Parameters.AddWithValue("$id", elemento.Id);
        comando.ExecuteNonQuery();
    }

    public void Eliminar(long id)
    {
        // La clave foránea tiene ON DELETE SET NULL: los productos quedan sin asignar.
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"DELETE FROM {Tabla} WHERE Id = $id";
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public int ContarProductos(long id)
    {
        using var db = conexion.Abrir();
        using var comando = db.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM Productos WHERE {ColumnaEnProductos} = $id";
        comando.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    private void CargarParametros(SqliteCommand comando, T elemento)
    {
        comando.Parameters.AddWithValue("$Nombre", elemento.Nombre);
        CargarParametrosExtra(comando, elemento);
    }
}

public sealed class SqliteCategoriaRepositorio(ConexionSqlite conexion) : SqliteCatalogoRepositorio<Categoria>(conexion)
{
    protected override string Tabla => "Categorias";
    protected override string ColumnaEnProductos => "CategoriaId";

    protected override Categoria Leer(SqliteDataReader lector) =>
        Categoria.Restaurar(lector.GetInt64(0), lector.GetString(1));
}

public sealed class SqliteProveedorRepositorio(ConexionSqlite conexion) : SqliteCatalogoRepositorio<Proveedor>(conexion)
{
    protected override string Tabla => "Proveedores";
    protected override string ColumnaEnProductos => "ProveedorId";
    protected override string[] ColumnasExtra => ["Telefono"];

    protected override Proveedor Leer(SqliteDataReader lector) =>
        Proveedor.Restaurar(lector.GetInt64(0), lector.GetString(1), lector.GetString(2));

    protected override void CargarParametrosExtra(SqliteCommand comando, Proveedor proveedor) =>
        comando.Parameters.AddWithValue("$Telefono", proveedor.Telefono);
}
