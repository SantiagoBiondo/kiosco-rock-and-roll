using KioscoRockAndRoll.Dominio.Entidades;

namespace Proyecto_Kiosco_Rock_Roll.Interfaz
{
    /// <summary>Una opción de una lista desplegable: el valor que importa y el texto que se ve.</summary>
    public sealed record Opcion<T>(T Valor, string Texto);

    /// <summary>Armado de las listas de categorías y proveedores que se usan en varias pantallas.</summary>
    public static class OpcionesDeCatalogo
    {
        /// <summary>Para asignar: "(ninguna)" más cada elemento.</summary>
        public static List<Opcion<long?>> ParaAsignar<T>(IEnumerable<T> elementos, string ninguno) where T : ElementoDeCatalogo =>
            [new(null, ninguno), .. elementos.Select(e => new Opcion<long?>(e.Id, e.Nombre))];

        /// <summary>Para filtrar: "Todas", "Sin asignar" y cada elemento.</summary>
        public static List<Opcion<FiltroClasificacion>> ParaFiltrar<T>(IEnumerable<T> elementos, string todos, string sinAsignar)
            where T : ElementoDeCatalogo =>
            [
                new(FiltroClasificacion.Todos, todos),
                new(FiltroClasificacion.SinAsignar, sinAsignar),
                .. elementos.Select(e => new Opcion<FiltroClasificacion>(FiltroClasificacion.Solo(e.Id), e.Nombre))
            ];
    }
}
