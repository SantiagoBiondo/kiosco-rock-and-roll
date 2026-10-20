namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>Un rango de días, inclusive en los dos extremos.</summary>
public sealed record PeriodoReporte
{
    public PeriodoReporte(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde)
            throw new ReglaDeNegocioException("La fecha \"hasta\" no puede ser anterior a \"desde\".");
        Desde = desde;
        Hasta = hasta;
    }

    public DateOnly Desde { get; }
    public DateOnly Hasta { get; }

    public string Descripcion => Desde == Hasta
        ? $"{Desde:dd/MM/yyyy}"
        : $"{Desde:dd/MM/yyyy} al {Hasta:dd/MM/yyyy}";

    public static PeriodoReporte Dia(DateOnly dia) => new(dia, dia);

    /// <summary>De lunes a hoy.</summary>
    public static PeriodoReporte SemanaHasta(DateOnly hoy)
    {
        var diasDesdeLunes = ((int)hoy.DayOfWeek + 6) % 7;
        return new(hoy.AddDays(-diasDesdeLunes), hoy);
    }

    /// <summary>Del 1 del mes a hoy.</summary>
    public static PeriodoReporte MesHasta(DateOnly hoy) => new(new DateOnly(hoy.Year, hoy.Month, 1), hoy);

    /// <summary>El mes calendario anterior completo.</summary>
    public static PeriodoReporte MesAnterior(DateOnly hoy)
    {
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        return new(inicio, inicio.AddMonths(1).AddDays(-1));
    }

    /// <summary>Del 1 de enero a hoy.</summary>
    public static PeriodoReporte AnioHasta(DateOnly hoy) => new(new DateOnly(hoy.Year, 1, 1), hoy);
}

public enum Agrupacion
{
    PorDia = 1,
    PorMes = 2
}

/// <summary>Montos de venta y costo, con la ganancia que resulta.</summary>
public abstract record ConGanancia(decimal Vendido, decimal Costo)
{
    public decimal Ganancia => Vendido - Costo;

    /// <summary>Ganancia sobre el costo (como se calcula en el editor de productos). Null si no hay costo.</summary>
    public decimal? PorcentajeSobreCosto => Costo == 0 ? null : Ganancia / Costo;
}

/// <summary>Lo vendido en un día (lo que guarda la base, sin ventas anuladas).</summary>
public sealed record VentasDeUnDia(DateOnly Dia, int CantidadVentas, decimal Vendido, decimal Costo) : ConGanancia(Vendido, Costo);

/// <summary>Una fila del reporte: un día o un mes.</summary>
public sealed record FilaDeReporte(string Etiqueta, DateOnly Inicio, int CantidadVentas, decimal Vendido, decimal Costo)
    : ConGanancia(Vendido, Costo);

/// <summary>Cuánto se vendió y ganó con un producto en el período.</summary>
public sealed record RendimientoProducto(long ProductoId, string Nombre, int Unidades, decimal Vendido, decimal Costo)
    : ConGanancia(Vendido, Costo);

public enum OrdenProductos
{
    PorUnidades = 1,
    PorRecaudacion = 2,
    PorGanancia = 3
}

/// <summary>El reporte de ventas y ganancia de un período.</summary>
public sealed class ReporteDeVentas
{
    private static readonly string[] Meses =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    private static readonly string[] DiasSemana = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"];

    private ReporteDeVentas(PeriodoReporte periodo, Agrupacion agrupacion,
        IReadOnlyList<FilaDeReporte> filas, IReadOnlyList<RendimientoProducto> productos)
    {
        Periodo = periodo;
        Agrupacion = agrupacion;
        Filas = filas;
        Productos = productos;
    }

    public PeriodoReporte Periodo { get; }
    public Agrupacion Agrupacion { get; }
    public IReadOnlyList<FilaDeReporte> Filas { get; }
    public IReadOnlyList<RendimientoProducto> Productos { get; }

    public int CantidadVentas => Filas.Sum(f => f.CantidadVentas);
    public decimal Vendido => Filas.Sum(f => f.Vendido);
    public decimal Costo => Filas.Sum(f => f.Costo);
    public decimal Ganancia => Vendido - Costo;
    public decimal? PorcentajeSobreCosto => Costo == 0 ? null : Ganancia / Costo;

    /// <summary>
    /// Arma el reporte. Todos los días (o meses) del período aparecen, aunque no haya habido ventas,
    /// para que se vean los días flojos.
    /// </summary>
    public static ReporteDeVentas Armar(PeriodoReporte periodo, Agrupacion agrupacion,
        IEnumerable<VentasDeUnDia> dias, IEnumerable<RendimientoProducto> productos)
    {
        var porDia = dias.ToDictionary(d => d.Dia);
        var filas = agrupacion == Agrupacion.PorMes
            ? AgruparPorMes(periodo, porDia)
            : CompletarDias(periodo, porDia);
        return new ReporteDeVentas(periodo, agrupacion, filas, productos.ToList());
    }

    /// <summary>Los productos en el orden pedido (de mayor a menor).</summary>
    public IReadOnlyList<RendimientoProducto> ProductosOrdenados(OrdenProductos orden) => (orden switch
    {
        OrdenProductos.PorUnidades => Productos.OrderByDescending(p => p.Unidades).ThenByDescending(p => p.Vendido),
        OrdenProductos.PorGanancia => Productos.OrderByDescending(p => p.Ganancia).ThenByDescending(p => p.Unidades),
        _ => Productos.OrderByDescending(p => p.Vendido).ThenByDescending(p => p.Unidades)
    }).ToList();

    private static List<FilaDeReporte> CompletarDias(PeriodoReporte periodo, Dictionary<DateOnly, VentasDeUnDia> porDia)
    {
        var filas = new List<FilaDeReporte>();
        for (var dia = periodo.Desde; dia <= periodo.Hasta; dia = dia.AddDays(1))
        {
            porDia.TryGetValue(dia, out var datos);
            filas.Add(new FilaDeReporte($"{DiasSemana[(int)dia.DayOfWeek]} {dia:dd/MM/yyyy}", dia,
                datos?.CantidadVentas ?? 0, datos?.Vendido ?? 0, datos?.Costo ?? 0));
        }
        return filas;
    }

    private static List<FilaDeReporte> AgruparPorMes(PeriodoReporte periodo, Dictionary<DateOnly, VentasDeUnDia> porDia)
    {
        var filas = new List<FilaDeReporte>();
        for (var mes = new DateOnly(periodo.Desde.Year, periodo.Desde.Month, 1); mes <= periodo.Hasta; mes = mes.AddMonths(1))
        {
            var delMes = porDia.Values.Where(d => d.Dia.Year == mes.Year && d.Dia.Month == mes.Month).ToList();
            filas.Add(new FilaDeReporte($"{Meses[mes.Month - 1]} {mes.Year}", mes,
                delMes.Sum(d => d.CantidadVentas), delMes.Sum(d => d.Vendido), delMes.Sum(d => d.Costo)));
        }
        return filas;
    }
}
