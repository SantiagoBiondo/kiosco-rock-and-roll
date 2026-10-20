namespace KioscoRockAndRoll.Dominio.Entidades;

public enum MedioDePago
{
    Efectivo = 1,
    Debito = 2,
    Credito = 3,
    TransferenciaQr = 4
}

public static class MedioDePagoExtensiones
{
    public static string Descripcion(this MedioDePago medio) => medio switch
    {
        MedioDePago.Efectivo => "Efectivo",
        MedioDePago.Debito => "Débito",
        MedioDePago.Credito => "Crédito",
        MedioDePago.TransferenciaQr => "Transferencia / QR",
        _ => medio.ToString()
    };
}
