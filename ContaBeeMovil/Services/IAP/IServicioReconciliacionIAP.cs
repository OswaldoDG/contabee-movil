using Contabee.Api.Ecommerce;

namespace ContaBeeMovil.Services.IAP;

/// <summary>
/// Reconciliación de compras in-app fuera de la Tienda: reintenta compras que se cobraron
/// pero no llegaron a acreditarse (verificación fallida, app cerrada, etc.). Pensado para
/// dispararse en el arranque y al reanudar la app, no solo al abrir la Tienda.
/// </summary>
public interface IServicioReconciliacionIAP
{
    /// <summary>
    /// Reconciliación silenciosa: reintenta la cola local de compras pendientes y restaura
    /// compras no consumidas desde la tienda. Auto-protegida (no hace nada sin cuenta fiscal
    /// activa o estando offline) e idempotente ante reentradas. Segura para arranque/resume.
    /// </summary>
    Task ReconciliarAsync();

    /// <summary>
    /// Persiste una compra que no pudo verificarse de inmediato para reintentarla después.
    /// </summary>
    void GuardarPendiente(DtoComprobanteCompra comprobante, string purchaseToken, string nombreProducto);
}
