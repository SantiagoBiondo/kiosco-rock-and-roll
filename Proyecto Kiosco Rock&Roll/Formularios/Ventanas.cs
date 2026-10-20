using KioscoRockAndRoll.Dominio.Entidades;
using Microsoft.Extensions.DependencyInjection;

namespace Proyecto_Kiosco_Rock_Roll.Formularios
{
    /// <summary>
    /// Abre las distintas pantallas. Los formularios dependen de esta interfaz
    /// y no de los otros formularios ni del contenedor de dependencias.
    /// </summary>
    public interface IVentanas
    {
        void MostrarInventario(IWin32Window duenio);
        void MostrarIngresoMercaderia(IWin32Window duenio);
        void MostrarVentasDelDia(IWin32Window duenio);

        /// <returns>El producto elegido, o null si se canceló.</returns>
        Producto? BuscarProducto(IWin32Window duenio);

        /// <returns>El producto creado, o null si se canceló.</returns>
        Producto? AltaProducto(IWin32Window duenio, string codigoBarras = "", int stockInicial = 0);

        /// <returns>El producto modificado, o null si se canceló.</returns>
        Producto? EditarProducto(IWin32Window duenio, Producto producto);

        /// <returns>El cobro confirmado, o null si se canceló.</returns>
        Cobro? PedirCobro(IWin32Window duenio, decimal total);
    }

    public sealed class Ventanas(IServiceProvider proveedor) : IVentanas
    {
        public void MostrarInventario(IWin32Window duenio) => MostrarDialogo<FormInventario>(duenio);

        public void MostrarIngresoMercaderia(IWin32Window duenio) => MostrarDialogo<FormIngresoMercaderia>(duenio);

        public void MostrarVentasDelDia(IWin32Window duenio) => MostrarDialogo<FormVentasDelDia>(duenio);

        public Producto? BuscarProducto(IWin32Window duenio)
        {
            using var form = ActivatorUtilities.CreateInstance<FormBuscarProducto>(proveedor);
            return form.ShowDialog(duenio) == DialogResult.OK ? form.Seleccionado : null;
        }

        public Producto? AltaProducto(IWin32Window duenio, string codigoBarras = "", int stockInicial = 0)
        {
            using var form = ActivatorUtilities.CreateInstance<FormProducto>(proveedor);
            form.PrepararAlta(codigoBarras, stockInicial);
            return form.ShowDialog(duenio) == DialogResult.OK ? form.Resultado : null;
        }

        public Producto? EditarProducto(IWin32Window duenio, Producto producto)
        {
            using var form = ActivatorUtilities.CreateInstance<FormProducto>(proveedor);
            form.PrepararEdicion(producto);
            return form.ShowDialog(duenio) == DialogResult.OK ? form.Resultado : null;
        }

        public Cobro? PedirCobro(IWin32Window duenio, decimal total)
        {
            using var form = new FormCobro(total);
            return form.ShowDialog(duenio) == DialogResult.OK ? form.Resultado : null;
        }

        private void MostrarDialogo<TForm>(IWin32Window duenio) where TForm : Form
        {
            using var form = ActivatorUtilities.CreateInstance<TForm>(proveedor);
            form.ShowDialog(duenio);
        }
    }
}
