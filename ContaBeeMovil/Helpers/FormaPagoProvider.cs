namespace ContaBeeMovil.Helpers;

public class FormaPago
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public static class FormaPagoProvider
{
    public static List<FormaPago> GetFormasPago() =>
    [
        new FormaPago { Codigo = "1",  Descripcion = "Efectivo" },
        new FormaPago { Codigo = "2",  Descripcion = "Cheque nominativo" },
        new FormaPago { Codigo = "3",  Descripcion = "Transferencia electrónica de fondos" },
        new FormaPago { Codigo = "4",  Descripcion = "Tarjeta de crédito" },
        new FormaPago { Codigo = "5",  Descripcion = "Monedero electrónico" },
        new FormaPago { Codigo = "6",  Descripcion = "Dinero electrónico" },
        new FormaPago { Codigo = "8",  Descripcion = "Vales de despensa" },
        new FormaPago { Codigo = "12", Descripcion = "Dación en pago" },
        new FormaPago { Codigo = "13", Descripcion = "Pago por subrogación" },
        new FormaPago { Codigo = "14", Descripcion = "Pago por consignación" },
        new FormaPago { Codigo = "15", Descripcion = "Condonación" },
        new FormaPago { Codigo = "17", Descripcion = "Compensación" },
        new FormaPago { Codigo = "23", Descripcion = "Novación" },
        new FormaPago { Codigo = "24", Descripcion = "Confusión" },
        new FormaPago { Codigo = "25", Descripcion = "Remisión de deuda" },
        new FormaPago { Codigo = "26", Descripcion = "Prescripción o caducidad" },
        new FormaPago { Codigo = "27", Descripcion = "A satisfacción del acreedor" },
        new FormaPago { Codigo = "28", Descripcion = "Tarjeta de débito" },
        new FormaPago { Codigo = "29", Descripcion = "Tarjeta de servicios" },
        new FormaPago { Codigo = "30", Descripcion = "Aplicación de anticipos" },
        new FormaPago { Codigo = "31", Descripcion = "Intermediario pagos" },
        new FormaPago { Codigo = "99", Descripcion = "Por definir" },
    ];
}
