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

    public async Task<CompraResultado> ComprarAsync(string productId)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
                return new CompraResultado(ResultadoCompra.SinConexion, null, "No se pudo conectar a la tienda");

            var compra = await billing.PurchaseAsync(productId, ItemType.InAppPurchase);
            if (compra is null)
                return new CompraResultado(ResultadoCompra.Cancelada, null, null);
            if (compra.State is PurchaseState.Deferred or PurchaseState.Purchasing)
                return new CompraResultado(ResultadoCompra.Pendiente, compra, null);
            return new CompraResultado(ResultadoCompra.Ok, compra, null);
        }
        catch (Exception ex) when (ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            return new CompraResultado(ResultadoCompra.Cancelada, null, null);
        }
        catch (Exception ex)
        {
            _logs.Log($"IAP: compra falló — {productId} — {ex.GetType().Name}: {ex.Message}");
            return new CompraResultado(ResultadoCompra.Error, null, ex.Message);
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

    public async Task<bool> ConsumirCompraAsync(string productId, string purchaseToken)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            var conectado = await billing.ConnectAsync();
            if (!conectado)
            {
                _logs.Log($"IAP: consumo — no se pudo conectar a la tienda ({productId})");
                return false;
            }

            await billing.ConsumePurchaseAsync(productId, purchaseToken);
            return true;
        }
        catch (Exception ex)
        {
            // Un consumible no consumido en Android no se puede recomprar y se reembolsa a los 3
            // días. Devolvemos false para que el llamador lo mantenga en cola y reintente el consumo.
            _logs.Log($"IAP: consumo falló — {productId} — {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }
}
