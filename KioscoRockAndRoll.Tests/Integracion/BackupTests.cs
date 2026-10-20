using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio.Servicios;

namespace KioscoRockAndRoll.Tests.Integracion;

public sealed class BackupTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly RelojManual _reloj = new(new DateTimeOffset(2026, 10, 20, 21, 30, 0, TimeSpan.Zero));
    private readonly GestorDeBackups _backups;
    private readonly InventarioServicio _inventario;
    private readonly string _carpetaExtra = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));

    public BackupTests()
    {
        _backups = new GestorDeBackups(_base.Conexion, new Ajustes(_base.Conexion), _reloj);
        _inventario = new InventarioServicio(new SqliteProductoRepositorio(_base.Conexion));
        _inventario.Crear(new DatosProducto("779", "Coca", 900, 1500, 10, 2));
    }

    public void Dispose()
    {
        _base.Dispose();
        try { Directory.Delete(_carpetaExtra, recursive: true); }
        catch (IOException) { }
    }

    private static InventarioServicio InventarioDe(string rutaArchivo) =>
        new(new SqliteProductoRepositorio(new ConexionSqlite(rutaArchivo)));

    [Fact]
    public void HacerBackup_CreaUnaCopiaLegibleDelDia()
    {
        var resultado = _backups.HacerBackup();

        Assert.True(resultado.TodoBien);
        var backup = Assert.Single(_backups.Listar());
        Assert.Equal("kiosco_2026-10-20.db", backup.Nombre);
        Assert.Equal("Coca", InventarioDe(backup.Ruta).ObtenerPorCodigo("779")!.Nombre);
    }

    [Fact]
    public void HacerBackup_DosVecesElMismoDia_ActualizaLaMismaCopia()
    {
        _backups.HacerBackup();
        _inventario.Crear(new DatosProducto("111", "Alfajor", 400, 700, 5, 1));

        _backups.HacerBackup();

        var backup = Assert.Single(_backups.Listar());
        Assert.NotNull(InventarioDe(backup.Ruta).ObtenerPorCodigo("111"));
    }

    [Fact]
    public void Retencion_ConservaSoloLosUltimos()
    {
        for (var dia = 0; dia < GestorDeBackups.CantidadAConservar + 5; dia++)
        {
            _backups.HacerBackup();
            _reloj.Avanzar(TimeSpan.FromDays(1));
        }

        var backups = _backups.Listar();
        Assert.Equal(GestorDeBackups.CantidadAConservar, backups.Count);
        Assert.Equal("kiosco_2026-11-23.db", backups[0].Nombre);   // el más nuevo primero
        Assert.Equal("kiosco_2026-10-25.db", backups[^1].Nombre);  // los 5 más viejos se borraron
    }

    [Fact]
    public void Retencion_NoTocaArchivosAjenos()
    {
        Directory.CreateDirectory(_backups.CarpetaPrincipal);
        var ajeno = Path.Combine(_backups.CarpetaPrincipal, "mis-notas.txt");
        File.WriteAllText(ajeno, "hola");

        for (var dia = 0; dia < GestorDeBackups.CantidadAConservar + 2; dia++)
        {
            _backups.HacerBackup();
            _reloj.Avanzar(TimeSpan.FromDays(1));
        }

        Assert.True(File.Exists(ajeno));
    }

    [Fact]
    public void CarpetaExtra_SeGuarda_YRecibeCopia()
    {
        Directory.CreateDirectory(_carpetaExtra);
        _backups.CarpetaExtra = _carpetaExtra;

        var resultado = _backups.HacerBackup();

        Assert.True(resultado.TodoBien);
        Assert.Equal(2, resultado.Destinos.Count);
        Assert.True(File.Exists(Path.Combine(_carpetaExtra, "kiosco_2026-10-20.db")));
        // El ajuste queda guardado en la base.
        Assert.Equal(_carpetaExtra, new GestorDeBackups(_base.Conexion, new Ajustes(_base.Conexion), _reloj).CarpetaExtra);
    }

    [Fact]
    public void CarpetaExtra_NoDisponible_InformaElError_PeroHaceLaPrincipal()
    {
        Directory.CreateDirectory(_carpetaExtra);
        _backups.CarpetaExtra = _carpetaExtra;
        Directory.Delete(_carpetaExtra); // como si se hubiera desconectado el pendrive

        var resultado = _backups.HacerBackup();

        Assert.False(resultado.TodoBien);
        Assert.True(resultado.Destinos[0].Exito);
        Assert.False(resultado.Destinos[1].Exito);
        Assert.False(Directory.Exists(_carpetaExtra)); // no la crea
        Assert.Single(_backups.Listar());
    }

    [Fact]
    public void CarpetaExtra_Inexistente_NoSePuedeElegir()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _backups.CarpetaExtra = Path.Combine(_carpetaExtra, "no-existe"));
    }

    [Fact]
    public void Restaurar_VuelveAlEstadoDelBackup_YGuardaElEstadoAnterior()
    {
        _backups.HacerBackup();
        var backup = _backups.Listar()[0];
        _inventario.Crear(new DatosProducto("111", "Alfajor", 400, 700, 5, 1));
        _reloj.Avanzar(TimeSpan.FromMinutes(5));

        _backups.Restaurar(backup.Ruta);

        Assert.Null(_inventario.ObtenerPorCodigo("111"));   // volvió al estado del backup
        Assert.NotNull(_inventario.ObtenerPorCodigo("779"));
        var anterior = Assert.Single(_backups.Listar(), b => b.Nombre.Contains("antes-de-restaurar"));
        Assert.NotNull(InventarioDe(anterior.Ruta).ObtenerPorCodigo("111")); // el estado previo quedó guardado
    }

    [Fact]
    public void Restaurar_ArchivoQueNoEsUnBackup_Falla_SinTocarLaBase()
    {
        var falso = Path.Combine(Path.GetTempPath(), $"falso_{Guid.NewGuid():N}.db");
        File.WriteAllText(falso, "esto no es una base de datos");
        try
        {
            Assert.ThrowsAny<Exception>(() => _backups.Restaurar(falso));
            Assert.NotNull(_inventario.ObtenerPorCodigo("779"));
        }
        finally
        {
            File.Delete(falso);
        }
    }

    [Fact]
    public void Migrar_UnaBaseConDatos_HaceUnaCopiaAntes()
    {
        // Una base en la versión 1: solo con la primera migración aplicada.
        var carpeta = Path.Combine(Path.GetTempPath(), "KioscoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            var conexion = new ConexionSqlite(Path.Combine(carpeta, "vieja.db"));
            using (var db = conexion.Abrir())
            using (var comando = db.CreateCommand())
            {
                comando.CommandText = InicializadorBaseDeDatos.Migraciones[0] + ";\nPRAGMA user_version = 1;";
                comando.ExecuteNonQuery();
            }
            var backups = new GestorDeBackups(conexion, new Ajustes(conexion), _reloj);

            new InicializadorBaseDeDatos(conexion, backups).Inicializar();

            var copia = Assert.Single(backups.Listar());
            Assert.Contains("antes-de-migrar-v1", copia.Nombre);
        }
        finally
        {
            try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void BaseNueva_NoHaceCopiaAlCrearse()
    {
        // _base ya se creó desde cero en el constructor; reinicializar con backups no copia nada.
        new InicializadorBaseDeDatos(_base.Conexion, _backups).Inicializar();
        Assert.Empty(_backups.Listar());
    }

    private sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _ahora = inicio;
        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
        public override DateTimeOffset GetUtcNow() => _ahora;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
