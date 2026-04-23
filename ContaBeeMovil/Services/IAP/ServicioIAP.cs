using ContaBeeMovil.Services.Dev;
using Plugin.InAppBilling;

namespace ContaBeeMovil.Services.IAP;

public class ServicioIAP : IServicioIAP
{
    private readonly IServicioLogs _logs;

    public ServicioIAP(IServicioLogs logs)
    {
        _logs = logs;
    }

    public async Task<IEnumerable<InAppBillingProduct>> ObtenerProductosAsync(IEnumerable<string> productIds)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            _logs.Log("IAP: ConnectAsync...");
            var conectado = await billing.ConnectAsync();
            _logs.Log($"IAP: conectado={conectado}");
            if (!conectado)
                return [];

            var ids = productIds.ToArray();
            _logs.Log($"IAP: consultando {ids.Length} productos uno por uno...");
            var resultados = new List<InAppBillingProduct>();
            foreach (var id in ids)
            {
                try
                {
                    var p = await billing.GetProductInfoAsync(ItemType.InAppPurchase, [id]);
                    if (p != null) resultados.AddRange(p);
                    _logs.Log($"IAP: {id} — encontrado");
                }
                catch
                {
                    _logs.Log($"IAP: {id} — no encontrado en tienda");
                }
            }
            _logs.Log($"IAP: {resultados.Count}/{ids.Length} productos disponibles en tienda");
            return resultados;
        }
        catch (Exception ex)
        {
            _logs.Log($"IAP: ObtenerProductos excepción — {ex.GetType().Name}: {ex.Message}");
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
