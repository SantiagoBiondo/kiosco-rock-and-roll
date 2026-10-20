using KioscoRockAndRoll.Dominio;
using KioscoRockAndRoll.Dominio.Entidades;

namespace KioscoRockAndRoll.Tests.Dominio;

public class PeriodoReporteTests
{
    private static readonly DateOnly Miercoles = new(2026, 10, 21);

    [Fact]
    public void Semana_VaDelLunesAHoy()
    {
        var periodo = PeriodoReporte.SemanaHasta(Miercoles);
        Assert.Equal(new DateOnly(2026, 10, 19), periodo.Desde);
        Assert.Equal(Miercoles, periodo.Hasta);
    }

    [Fact]
    public void Semana_UnDomingo_EmpiezaElLunesAnterior()
    {
        var domingo = new DateOnly(2026, 10, 25);
        Assert.Equal(new DateOnly(2026, 10, 19), PeriodoReporte.SemanaHasta(domingo).Desde);
    }

    [Fact]
    public void Mes_VaDelUnoAHoy()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), PeriodoReporte.MesHasta(Miercoles).Desde);
    }

    [Fact]
    public void MesAnterior_EsElMesCompleto()
    {
        var periodo = PeriodoReporte.MesAnterior(Miercoles);
        Assert.Equal(new DateOnly(2026, 9, 1), periodo.Desde);
        Assert.Equal(new DateOnly(2026, 9, 30), periodo.Hasta);

        var enero = PeriodoReporte.MesAnterior(new DateOnly(2027, 1, 15));
        Assert.Equal(new DateOnly(2026, 12, 1), enero.Desde);
        Assert.Equal(new DateOnly(2026, 12, 31), enero.Hasta);
    }

    [Fact]
    public void HastaAntesQueDesde_Falla()
    {
        Assert.Throws<ReglaDeNegocioException>(() => new PeriodoReporte(Miercoles, Miercoles.AddDays(-1)));
    }
}

public class ReporteDeVentasTests
{
    private static readonly PeriodoReporte TresDias = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));

    private static VentasDeUnDia Dia(int dia, int ventas, decimal vendido, decimal costo) =>
        new(new DateOnly(2026, 10, dia), ventas, vendido, costo);

    [Fact]
    public void PorDia_IncluyeLosDiasSinVentas()
    {
        var reporte = ReporteDeVentas.Armar(TresDias, Agrupacion.PorDia,
            [Dia(1, 2, 1000, 600), Dia(3, 1, 500, 300)], []);

        Assert.Equal(3, reporte.Filas.Count);
        Assert.Equal(0, reporte.Filas[1].Vendido);
        Assert.Equal("jue 01/10/2026", reporte.Filas[0].Etiqueta);
    }

    [Fact]
    public void Totales_YGanancia()
    {
        var reporte = ReporteDeVentas.Armar(TresDias, Agrupacion.PorDia,
            [Dia(1, 2, 1000, 600), Dia(3, 1, 500, 300)], []);

        Assert.Equal(3, reporte.CantidadVentas);
        Assert.Equal(1500, reporte.Vendido);
        Assert.Equal(900, reporte.Costo);
        Assert.Equal(600, reporte.Ganancia);
        Assert.Equal(600m / 900m, reporte.PorcentajeSobreCosto);
    }

    [Fact]
    public void PorMes_AgrupaYMuestraTodosLosMeses()
    {
        var periodo = new PeriodoReporte(new DateOnly(2026, 8, 15), new DateOnly(2026, 10, 10));
        var reporte = ReporteDeVentas.Armar(periodo, Agrupacion.PorMes,
        [
            new VentasDeUnDia(new DateOnly(2026, 8, 20), 1, 100, 50),
            new VentasDeUnDia(new DateOnly(2026, 10, 2), 2, 300, 200),
            new VentasDeUnDia(new DateOnly(2026, 10, 9), 1, 200, 100)
        ], []);

        Assert.Equal(["Agosto 2026", "Septiembre 2026", "Octubre 2026"], reporte.Filas.Select(f => f.Etiqueta));
        Assert.Equal(0, reporte.Filas[1].Vendido);
        Assert.Equal(500, reporte.Filas[2].Vendido);
        Assert.Equal(3, reporte.Filas[2].CantidadVentas);
    }

    [Fact]
    public void SinCosto_ElPorcentajeEsNull()
    {
        var fila = new FilaDeReporte("x", new DateOnly(2026, 1, 1), 1, 100, 0);
        Assert.Null(fila.PorcentajeSobreCosto);
    }

    [Fact]
    public void Productos_SeOrdenanSegunLoPedido()
    {
        var reporte = ReporteDeVentas.Armar(TresDias, Agrupacion.PorDia, [],
        [
            new RendimientoProducto(1, "Coca", Unidades: 10, Vendido: 15000, Costo: 9000),     // ganancia 6000
            new RendimientoProducto(2, "Chicle", Unidades: 40, Vendido: 8000, Costo: 2000),    // ganancia 6000... empate
            new RendimientoProducto(3, "Cigarrillos", Unidades: 5, Vendido: 20000, Costo: 18000) // ganancia 2000
        ]);

        Assert.Equal(["Chicle", "Coca", "Cigarrillos"], reporte.ProductosOrdenados(OrdenProductos.PorUnidades).Select(p => p.Nombre));
        Assert.Equal(["Cigarrillos", "Coca", "Chicle"], reporte.ProductosOrdenados(OrdenProductos.PorRecaudacion).Select(p => p.Nombre));
        Assert.Equal(["Chicle", "Coca", "Cigarrillos"], reporte.ProductosOrdenados(OrdenProductos.PorGanancia).Select(p => p.Nombre));
    }
}
