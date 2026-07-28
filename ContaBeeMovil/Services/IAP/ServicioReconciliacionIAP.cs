using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using Plugin.InAppBilling;

namespace ContaBeeMovil.Services.IAP;

public class ServicioReconciliacionIAP : IServicioReconciliacionIAP
{
    private readonly IServicioIAP _servicioIAP;
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioLogs _logs;

    private const string PrefsKeyComprasPendientes = "tienda.compras_pendientes";

    // La pasarela que aplica en esta plataforma (misma lógica que TiendaPage).
#if IOS || MACCATALYST
    private const PasarelaPago PasarelaPlataforma = PasarelaPago.Apple;
#elif ANDROID
    private const PasarelaPago PasarelaPlataforma = PasarelaPago.Google;
#else
    private const PasarelaPago PasarelaPlataforma = PasarelaPago.Interbancario;
#endif

    private bool _reconciliando;

    public ServicioReconciliacionIAP(
        IServicioIAP servicioIAP,
        IServicioEcommerce servicioEcommerce,
        IServicioSesion servicioSesion,
        IServicioLogs logs)
    {
        _servicioIAP       = servicioIAP;
        _servicioEcommerce = servicioEcommerce;
        _servicioSesion    = servicioSesion;
        _logs              = logs;
    }

    public async Task ReconciliarAsync()
    {
        if (_reconciliando) return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null || AppState.Instance.ModoOffline)
            return;

        _reconciliando = true;
        try
        {
            await ReintentarPendientesLocalesAsync(cuenta.CuentaFiscalId);
            await RestaurarDesdeStoreAsync(cuenta.CuentaFiscalId);
        }
        catch (Exception ex)
        {
            _logs.Log($"ReconciliacionIAP: excepción — {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _reconciliando = false;
        }
    }

    // ── Cola local de compras pendientes ──────────────────────────────────────

    public void GuardarPendiente(DtoComprobanteCompra comprobante, string purchaseToken, string nombreProducto)
    {
        try
        {
            var pendientes = LeerPendientes();
            if (pendientes.Any(p => p.Comprobante.CompraId == comprobante.CompraId))
            {
                _logs.Log($"ReconciliacionIAP: compra ya estaba en pendientes locales — {comprobante.ProductoTiendaId}");
                return;
            }
            pendientes.Add(new CompraPendienteLocal { Comprobante = comprobante, PurchaseToken = purchaseToken, NombreProducto = nombreProducto });
            Preferences.Default.Set(PrefsKeyComprasPendientes, System.Text.Json.JsonSerializer.Serialize(pendientes));
            _logs.Log($"ReconciliacionIAP: compra pendiente guardada localmente — {comprobante.ProductoTiendaId}");
        }
        catch (Exception ex)
        {
            _logs.Log($"ReconciliacionIAP: error guardando compra pendiente local — {ex.Message}");
        }
    }

    private static List<CompraPendienteLocal> LeerPendientes()
    {
        try
        {
            var json = Preferences.Default.Get(PrefsKeyComprasPendientes, null as string);
            if (string.IsNullOrEmpty(json)) return [];
            return System.Text.Json.JsonSerializer.Deserialize<List<CompraPendienteLocal>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task ReintentarPendientesLocalesAsync(Guid cfidActual)
    {
        var pendientes = LeerPendientes();
        if (pendientes.Count == 0) return;

        _logs.Log($"ReconciliacionIAP: {pendientes.Count} compras pendientes locales — reintentando");
        var remanentes = new List<CompraPendienteLocal>();

        foreach (var p in pendientes)
        {
            if (!Guid.TryParse(p.Comprobante.CuentaFiscalId, out var cfid))
                cfid = cfidActual;

            var verificado = await _servicioEcommerce.VerificarCompraIAP(cfid, p.Comprobante);
            bool completado = verificado && await _servicioEcommerce.CompletarCompraIAP(cfid, p.Comprobante);

            _logs.Log($"ReconciliacionIAP: reintento local — producto={p.Comprobante.ProductoTiendaId} verificado={verificado} completado={completado}");

            if (completado)
            {
                var consumido = await _servicioIAP.ConsumirCompraAsync(p.Comprobante.ProductoTiendaId, p.PurchaseToken);
                await _servicioSesion.GetLicenciaAsync();
                if (consumido)
                {
                    _logs.Log($"ReconciliacionIAP: reintento local exitoso — {p.Comprobante.ProductoTiendaId}");
                }
                else
                {
                    // Acreditado pero sin consumir: se mantiene en cola para reintentar el consumo.
                    _logs.Log($"ReconciliacionIAP: acreditado pero consumo falló, se mantiene en cola — {p.Comprobante.ProductoTiendaId}");
                    remanentes.Add(p);
                }
            }
            else
            {
                remanentes.Add(p);
            }
        }

        if (remanentes.Count != pendientes.Count)
        {
            if (remanentes.Count == 0)
                Preferences.Default.Remove(PrefsKeyComprasPendientes);
            else
                Preferences.Default.Set(PrefsKeyComprasPendientes, System.Text.Json.JsonSerializer.Serialize(remanentes));
        }
    }

    // ── Restauración desde la tienda (GetPurchases) ───────────────────────────

    private async Task RestaurarDesdeStoreAsync(Guid cfid)
    {
        var pendientes = (await _servicioIAP.RestaurarComprasAsync())
            .Where(c => c.State == PurchaseState.Purchased && c.IsAcknowledged != true)
            .ToList();

        if (pendientes.Count == 0) return;
        _logs.Log($"ReconciliacionIAP: {pendientes.Count} compras no consumidas en la tienda");

        var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

        // Precio reportado por la store (MicrosPrice) por SKU, consultado una sola vez para todos los
        // IDs restaurados. En restauración es el precio ACTUAL, que para una compra vieja podría diferir
        // de lo cobrado; aun así es mejor que registrar 0.
        var idsProductos = pendientes.Select(c => c.ProductId).Distinct().ToArray();
        var preciosStore = new Dictionary<string, double>();
        foreach (var sp in await _servicioIAP.ObtenerProductosAsync(idsProductos))
            preciosStore[sp.ProductId] = sp.MicrosPrice / 1_000_000.0;

        foreach (var compra in pendientes)
        {
            var montoStore = preciosStore.TryGetValue(compra.ProductId, out var m) ? m : 0;
            var comprobante = await ConstruirComprobanteMinimoAsync(compra, cfid, dispositivoId, montoStore);
            if (comprobante is null)
            {
                _logs.Log($"ReconciliacionIAP: sin verificationData para {compra.ProductId} — se omite");
                continue;
            }

            var verificado = await _servicioEcommerce.VerificarCompraIAP(cfid, comprobante);
            bool completado = verificado && await _servicioEcommerce.CompletarCompraIAP(cfid, comprobante);
            _logs.Log($"ReconciliacionIAP: restore — producto={compra.ProductId} verificado={verificado} completado={completado}");

            if (completado)
            {
                var consumido = await _servicioIAP.ConsumirCompraAsync(compra.ProductId, compra.PurchaseToken ?? compra.TransactionIdentifier);
                await _servicioSesion.GetLicenciaAsync();
                if (!consumido)
                    _logs.Log($"ReconciliacionIAP: acreditado pero consumo falló — {compra.ProductId} (se reintentará en el próximo restore)");
            }
            else
            {
                GuardarPendiente(comprobante, compra.PurchaseToken ?? compra.TransactionIdentifier, compra.ProductId);
            }
        }
    }

    // El backend deriva el producto acreditado del ProductoTiendaId (SKU validado), así que aquí no
    // se necesita el catálogo (Elementos vacío). El monto sí se toma del precio reportado por la store
    // (montoStore) para no registrar 0 en compras recuperadas por restauración.
    private async Task<DtoComprobanteCompra?> ConstruirComprobanteMinimoAsync(InAppBillingPurchase compra, Guid cfid, string dispositivoId, double montoStore)
    {
        var verificationData = await ObtenerVerificationDataAsync(compra);
        if (string.IsNullOrEmpty(verificationData)) return null;

        return new DtoComprobanteCompra
        {
            CuentaFiscalId   = cfid.ToString(),
            DispositivoId    = dispositivoId,
            PasarelaPago     = PasarelaPlataforma,
            PasarelaId       = verificationData,
            CompraId         = compra.TransactionIdentifier,
            ProductoTiendaId = compra.ProductId,
            MontoCompra      = montoStore,
            Elementos        = [],
        };
    }

    private async Task<string?> ObtenerVerificationDataAsync(InAppBillingPurchase compra)
    {
#if IOS || MACCATALYST
        // Si el recibo falta (install limpia / sandbox) se fuerza un refresh (C6).
        return await ReceiptApple.LeerBase64ConRefrescoAsync(_logs) ?? compra.OriginalJson;
#elif ANDROID
        return await Task.FromResult(compra.PurchaseToken);
#else
        return await Task.FromResult(compra.TransactionIdentifier);
#endif
    }

    private sealed class CompraPendienteLocal
    {
        public DtoComprobanteCompra Comprobante { get; set; } = null!;
        public string PurchaseToken { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
    }
}
