using Plugin.InAppBilling;

namespace ContaBeeMovil.Services.IAP;

public interface IServicioIAP
{
    Task<IEnumerable<InAppBillingProduct>> ObtenerProductosAsync(IEnumerable<string> productIds);
    Task<InAppBillingPurchase?> ComprarAsync(string productId);
}
