namespace KioscoRockAndRoll.Dominio.Entidades;

/// <summary>
/// Un turno de caja: se abre con un efectivo inicial y se cierra contando el efectivo.
/// Las ventas hechas mientras está abierto le pertenecen.
/// </summary>
public sealed class Turno
{
    private Turno(long id, DateTime apertura, decimal efectivoInicial, DateTime? cierre, decimal? efectivoContado)
    {
        Id = id;
        Apertura = apertura;
        EfectivoInicial = efectivoInicial;
        Cierre = cierre;
        EfectivoContado = efectivoContado;
    }

    public long Id { get; private set; }
    public DateTime Apertura { get; }
    public decimal EfectivoInicial { get; }
    public DateTime? Cierre { get; private set; }

    /// <summary>El efectivo que había en la caja al cerrarla (null mientras está abierta).</summary>
    public decimal? EfectivoContado { get; private set; }

    public bool EstaAbierto => Cierre is null;

    public static Turno Abrir(decimal efectivoInicial, DateTime ahora)
    {
        if (efectivoInicial < 0)
            throw new ReglaDeNegocioException("El efectivo inicial no puede ser negativo.");
        return new Turno(0, ahora, efectivoInicial, null, null);
    }

    public static Turno Restaurar(long id, DateTime apertura, decimal efectivoInicial, DateTime? cierre, decimal? efectivoContado) =>
        new(id, apertura, efectivoInicial, cierre, efectivoContado);

    public void AsignarId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("El turno ya tiene un Id asignado.");
        Id = id;
    }

    public void Cerrar(decimal efectivoContado, DateTime ahora)
    {
        if (!EstaAbierto)
            throw new ReglaDeNegocioException("La caja ya está cerrada.");
        if (efectivoContado < 0)
            throw new ReglaDeNegocioException("El efectivo contado no puede ser negativo.");
        Cierre = ahora;
        EfectivoContado = efectivoContado;
    }
}

public enum TipoMovimientoCaja
{
    Ingreso = 1,
    Retiro = 2
}

public static class TipoMovimientoCajaExtensiones
{
    public static string Descripcion(this TipoMovimientoCaja tipo) => tipo switch
    {
        TipoMovimientoCaja.Ingreso => "Ingreso",
        TipoMovimientoCaja.Retiro => "Retiro",
        _ => tipo.ToString()
    };
}

/// <summary>
/// Efectivo que entra o sale de la caja sin ser una venta
/// (cambio que se trae, pago a un proveedor, retiro del dueño...).
/// </summary>
public sealed class MovimientoCaja
{
    public const int LargoMaximoMotivo = 100;

    private MovimientoCaja(long id, DateTime fecha, TipoMovimientoCaja tipo, decimal monto, string motivo)
    {
        Id = id;
        Fecha = fecha;
        Tipo = tipo;
        Monto = monto;
        Motivo = motivo;
    }

    public long Id { get; private set; }
    public DateTime Fecha { get; }
    public TipoMovimientoCaja Tipo { get; }
    public decimal Monto { get; }
    public string Motivo { get; }

    public string TipoDescripcion => Tipo.Descripcion();

    /// <summary>Cuánto cambia el efectivo de la caja: positivo si entra, negativo si sale.</summary>
    public decimal Efecto => Tipo == TipoMovimientoCaja.Ingreso ? Monto : -Monto;

    public static MovimientoCaja Crear(TipoMovimientoCaja tipo, decimal monto, string motivo, DateTime fecha)
    {
        motivo = motivo?.Trim() ?? "";
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo));
        if (monto <= 0)
            throw new ReglaDeNegocioException("El monto tiene que ser mayor a cero.");
        if (motivo == "")
            throw new ReglaDeNegocioException("Escribí el motivo (por ejemplo: \"pago a proveedor\").");
        if (motivo.Length > LargoMaximoMotivo)
            throw new ReglaDeNegocioException($"El motivo no puede tener más de {LargoMaximoMotivo} letras.");
        return new MovimientoCaja(0, fecha, tipo, monto, motivo);
    }

    public static MovimientoCaja Restaurar(long id, DateTime fecha, TipoMovimientoCaja tipo, decimal monto, string motivo) =>
        new(id, fecha, tipo, monto, motivo);

    public void AsignarId(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException("El movimiento ya tiene un Id asignado.");
        Id = id;
    }
}

/// <summary>Lo cobrado en un turno con un medio de pago, y en cuántas ventas se usó ese medio.</summary>
public sealed record TotalPorMedio(MedioDePago Medio, int CantidadVentas, decimal Total);

/// <summary>
/// Las cuentas de un turno: cuánto efectivo debería haber en la caja y, si ya se cerró,
/// la diferencia con lo que se contó.
/// </summary>
/// <param name="ventas">Lo cobrado con cada medio. Una venta pagada con varios medios aparece en cada uno.</param>
/// <param name="cantidadVentas">Cuántas ventas hubo (no se puede sumar desde <paramref name="ventas"/> por los pagos mixtos).</param>
public sealed class ResumenTurno(
    Turno turno, IReadOnlyList<TotalPorMedio> ventas, IReadOnlyList<MovimientoCaja> movimientos, int cantidadVentas)
{
    public Turno Turno { get; } = turno;
    public IReadOnlyList<TotalPorMedio> Ventas { get; } = ventas;
    public IReadOnlyList<MovimientoCaja> Movimientos { get; } = movimientos;

    public decimal TotalVendido => Ventas.Sum(v => v.Total);
    public int CantidadVentas { get; } = cantidadVentas;
    public decimal VentasEnEfectivo => Ventas.Where(v => v.Medio == MedioDePago.Efectivo).Sum(v => v.Total);
    public decimal Ingresos => Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Monto);
    public decimal Retiros => Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Retiro).Sum(m => m.Monto);

    /// <summary>El efectivo que debería haber en la caja.</summary>
    public decimal EfectivoEsperado => Turno.EfectivoInicial + VentasEnEfectivo + Ingresos - Retiros;

    /// <summary>Contado menos esperado: positivo si sobra, negativo si falta. Null si el turno sigue abierto.</summary>
    public decimal? Diferencia => Turno.EfectivoContado - EfectivoEsperado;

    public decimal DiferenciaSiSeContara(decimal efectivoContado) => efectivoContado - EfectivoEsperado;

    // Para mostrar en listados.
    public DateTime Apertura => Turno.Apertura;
    public DateTime? Cierre => Turno.Cierre;
    public decimal EfectivoInicial => Turno.EfectivoInicial;
    public decimal? EfectivoContado => Turno.EfectivoContado;

    /// <summary>"Exacto", "Sobran $ 100,00" o "Faltan $ 50,00".</summary>
    public static string DescribirDiferencia(decimal diferencia) => diferencia switch
    {
        0 => "Exacto",
        > 0 => $"Sobran {diferencia:C2}",
        _ => $"Faltan {-diferencia:C2}"
    };
}
