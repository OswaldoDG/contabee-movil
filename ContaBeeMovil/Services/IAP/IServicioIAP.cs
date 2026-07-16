using Plugin.InAppBilling;

namespace ContaBeeMovil.Services.IAP;

public interface IServicioIAP
{
    Task<IEnumerable<InAppBillingProduct>> ObtenerProductosAsync(IEnumerable<string> productIds);
    Task<CompraResultado> ComprarAsync(string productId);
    Task<bool> ConsumirCompraAsync(string productId, string purchaseToken);
    Task<IEnumerable<InAppBillingPurchase>> RestaurarComprasAsync();
}

/// <summary>
/// Desenlace de un intento de compra. Distingue la cancelación del usuario de un fallo de red o
/// una compra que quedó pendiente de aprobación (Ask to Buy / SCA), que antes se confundían con "null".
/// </summary>
public enum ResultadoCompra { Ok, Cancelada, SinConexion, Pendiente, Error }

public sealed record CompraResultado(ResultadoCompra Estado, InAppBillingPurchase? Compra, string? Detalle);
