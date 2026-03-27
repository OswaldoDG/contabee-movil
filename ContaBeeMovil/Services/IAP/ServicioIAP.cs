using Plugin.InAppBilling;

namespace ContaBeeMovil.Services.IAP;

public class ServicioIAP : IServicioIAP
{
    public async Task<IEnumerable<InAppBillingProduct>> ObtenerProductosAsync(IEnumerable<string> productIds)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
                return [];

            var productos = await billing.GetProductInfoAsync(ItemType.InAppPurchase, productIds.ToArray());
            return productos ?? [];
        }
        catch
        {
            return [];
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    public async Task<InAppBillingPurchase?> ComprarAsync(string productId)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
                return null;

            var compra = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);
            return compra;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }
}
