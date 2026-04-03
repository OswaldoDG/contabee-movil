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

    public async Task<IEnumerable<InAppBillingPurchase>> RestaurarComprasAsync()
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
                return [];

            var compras = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
            return compras ?? [];
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

    public async Task ConsumirCompraAsync(string productId, string purchaseToken)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
                return;

            await billing.ConsumePurchaseAsync(productId, purchaseToken);
        }
        catch
        {
            // Si falla el consumo no bloqueamos al usuario — el backend ya acreditó
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }
}
