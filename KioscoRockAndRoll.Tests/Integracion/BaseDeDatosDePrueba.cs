using KioscoRockAndRoll.Datos;

namespace KioscoRockAndRoll.Tests.Integracion;

/// <summary>Crea una base SQLite temporal y vacía para cada test, y la borra al terminar.</summary>
public sealed class BaseDeDatosDePrueba : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));

    public BaseDeDatosDePrueba()
    {
        Conexion = new ConexionSqlite(Path.Combine(_carpeta, "kiosco.db"));
        new InicializadorBaseDeDatos(Conexion).Inicializar();
    }

    public ConexionSqlite Conexion { get; }

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si queda algún archivo tomado, se limpia en la próxima */ }
    }
}
