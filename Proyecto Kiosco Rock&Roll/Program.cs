using KioscoRockAndRoll.Datos;
using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Servicios;
using Microsoft.Extensions.DependencyInjection;
using Proyecto_Kiosco_Rock_Roll.Interfaz;
using Proyecto_Kiosco_Rock_Roll.Interfaz.Vistas;

namespace Proyecto_Kiosco_Rock_Roll
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            ConfigurarManejoDeErrores();

            using var proveedor = ConfigurarServicios().BuildServiceProvider(validateScopes: true);
            proveedor.GetRequiredService<InicializadorBaseDeDatos>().Inicializar();

            Application.Run(proveedor.GetRequiredService<FormPrincipal>());
        }

        /// <summary>
        /// Raíz de composición: el único lugar donde se decide qué implementación usa cada interfaz.
        /// </summary>
        private static ServiceCollection ConfigurarServicios()
        {
            // La base se guarda en %LOCALAPPDATA%\KioscoRockAndRoll\kiosco.db.
            // Para hacer un backup alcanza con copiar ese archivo.
            var rutaBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KioscoRockAndRoll", "kiosco.db");

            var servicios = new ServiceCollection();
            servicios.AddDatosSqlite(rutaBase);
            servicios.AddSingleton(TimeProvider.System);
            servicios.AddSingleton<InventarioServicio>();
            servicios.AddSingleton<VentaServicio>();

            // Una sola ventana con una instancia de cada vista durante toda la ejecución.
            servicios.AddSingleton<FormPrincipal>();
            servicios.AddSingleton<VistaVenta>();
            servicios.AddSingleton<VistaInventario>();
            servicios.AddSingleton<VistaIngresoMercaderia>();
            servicios.AddSingleton<VistaVentasDelDia>();
            return servicios;
        }

        // Red de seguridad: si algo falla y nadie lo atrapó, se muestra un mensaje en vez de cerrar el programa.
        private static void ConfigurarManejoDeErrores()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                if (e.Exception is ReglaDeNegocioException regla)
                    Mensajes.Advertencia(null, regla.Message);
                else
                    Mensajes.Error(null, $"Ocurrió un error inesperado:\n{e.Exception.Message}");
            };
        }
    }
}
