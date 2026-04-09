using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.IAP;
using Plugin.InAppBilling;

namespace ContaBeeMovil.Pages.Tienda;

public partial class TiendaPage : ContentPage
{
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioIAP _servicioIAP;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;

    private Grid? _loadingOverlay;
    private CollectionView? _listaProductos;
    private Border? _bannerProximamente;

    // Catálogo del backend guardado para usarse al verificar la compra
    private List<DtoProducto> _productosCreditos = [];

    public TiendaPage(IServicioEcommerce servicioEcommerce, IServicioIAP servicioIAP, IServicioSesion servicioSesion, IServicioAlerta servicioAlerta, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioEcommerce = servicioEcommerce;
        _servicioIAP       = servicioIAP;
        _servicioSesion    = servicioSesion;
        _servicioAlerta    = servicioAlerta;
        _logs              = logs;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _loadingOverlay     = this.FindByName<Grid>("LoadingOverlay");
        _listaProductos     = this.FindByName<CollectionView>("ListaProductos");
        _bannerProximamente = this.FindByName<Border>("BannerProximamente");

        _logs.Log("Tienda: página abierta");
        await CargarProductosAsync();
        await RestaurarComprasPendientesAsync();
    }

    // ── Carga del catálogo ────────────────────────────────────────────────────

    private async Task CargarProductosAsync()
    {
        _logs.Log("Tienda: iniciando carga de catálogo");
        SetCargando(true);
        try
        {
            var resultado = await _servicioEcommerce.GetCatalogoProductos();
            if (!resultado.Ok || resultado.Payload is null)
            {
                _logs.Log($"Tienda: error al obtener catálogo — {resultado.Error?.Mensaje ?? "sin detalle"}");
                await _servicioAlerta.MostrarAsync("Error", "No se pudo obtener el catálogo.", verBotonCancelar: false, confirmarText: "Aceptar");
                return;
            }

            var categoriaCreditos = resultado.Payload.FirstOrDefault(c => c.Clave == "CREDITOS");
            if (categoriaCreditos?.Productos is null)
            {
                _logs.Log("Tienda: categoría CREDITOS no encontrada en el catálogo");
                return;
            }

            _productosCreditos = categoriaCreditos.Productos
                .Where(p => p.Propiedades.Any(x => x.Propiedad == "credcaptura" && x.Valor == "true"))
                .Where(p => p.Precios.Any(pr => pr.Tipo == TipoPrecio.Publico && pr.Precio > 0))
                .OrderBy(p =>
                {
                    var prop = p.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto");
                    return int.TryParse(prop?.Valor, out var u) ? u : 0;
                })
                .ToList();

            _logs.Log($"Tienda: {_productosCreditos.Count} productos encontrados en backend");

            var iapIds = _productosCreditos.Select(p => $"contabee.creditos.{p.Clave.ToLower()}").ToArray();
            _logs.Log($"Tienda: consultando store con IDs [{string.Join(", ", iapIds)}]");

            var productosStore = (await _servicioIAP.ObtenerProductosAsync(iapIds)).ToList();
            var disponibleEnTienda = productosStore.Count > 0;
            _logs.Log($"Tienda: store respondió {productosStore.Count} productos — disponible={disponibleEnTienda}");

            List<ProductoIAPModel> modelos;

            if (disponibleEnTienda)
            {
                modelos = productosStore.Select(sp => new ProductoIAPModel
                {
                    Clave              = sp.ProductId,
                    Nombre             = sp.Name.Contains('(') ? sp.Name[..sp.Name.IndexOf('(')].Trim() : sp.Name,
                    Unidades           = ObtenerUnidades(sp.ProductId),
                    PrecioTexto        = sp.LocalizedPrice,
                    PrecioValor        = sp.MicrosPrice / 1_000_000.0,
                    DisponibleEnTienda = true
                })
                .OrderBy(m => m.PrecioValor)
                .ToList();
            }
            else
            {
                modelos = _productosCreditos.Select(p =>
                {
                    var precio   = p.Precios.First(pr => pr.Tipo == TipoPrecio.Publico);
                    var unidades = p.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";
                    return new ProductoIAPModel
                    {
                        Clave              = $"contabee.creditos.{p.Clave.ToLower()}",
                        Nombre             = p.Nombre,
                        Unidades           = unidades,
                        PrecioTexto        = $"${precio.Precio:N2} MXN",
                        PrecioValor        = precio.Precio,
                        DisponibleEnTienda = false
                    };
                })
                .OrderBy(m => m.PrecioValor)
                .ToList();
            }

            if (_listaProductos is not null)
                _listaProductos.ItemsSource = modelos;

            if (_bannerProximamente is not null)
                _bannerProximamente.IsVisible = !disponibleEnTienda;
        }
        finally
        {
            SetCargando(false);
        }
    }

    // ── Restaurar compras pendientes (equivalente a restorePurchasesOnStart) ──

    private async Task RestaurarComprasPendientesAsync()
    {
        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null) return;

        _logs.Log("Tienda: restaurando compras pendientes");
        var comprasPendientes = (await _servicioIAP.RestaurarComprasAsync())
            .Where(c => c.State == PurchaseState.Purchased && c.IsAcknowledged != true)
            .ToList();

        _logs.Log($"Tienda: {comprasPendientes.Count} compras pendientes encontradas");

        foreach (var compra in comprasPendientes)
        {
            var productoCatalogo = BuscarProductoEnCatalogo(compra.ProductId);
            if (productoCatalogo is null)
            {
                _logs.Log($"Tienda: restore — producto no encontrado en catálogo: {compra.ProductId}");
                continue;
            }
            await ProcesarCompraAsync(compra, productoCatalogo, cuenta.CuentaFiscalId, silencioso: true);
        }
    }

    // ── Máquina de estados (equivalente a handlePurchase + completePurchase + updateLicense) ──

    private async Task<bool> ProcesarCompraAsync(InAppBillingPurchase compra, DtoProducto productoCatalogo, Guid cfid, bool silencioso = false)
    {
        _logs.Log($"Tienda: procesando compra" +
                  $" | Id={compra.Id}" +
                  $" | TransactionIdentifier={compra.TransactionIdentifier}" +
                  $" | OriginalTransactionIdentifier={compra.OriginalTransactionIdentifier}" +
                  $" | ProductId={compra.ProductId}" +
                  $" | State={compra.State}" +
                  $" | PurchaseToken={compra.PurchaseToken}" +
                  $" | IsAcknowledged={compra.IsAcknowledged}" +
                  $" | AutoRenewing={compra.AutoRenewing}" +
                  $" | TransactionDateUtc={compra.TransactionDateUtc}" +
                  $" | Payload={compra.Payload}" +
                  $" | Signature={compra.Signature}" +
                  $" | OriginalJson={compra.OriginalJson}");

        switch (compra.State)
        {
            case PurchaseState.Purchased:
                return await EnviarAlBackendYCompletarAsync(compra, productoCatalogo, cfid, silencioso);

            case PurchaseState.Purchasing:
            case PurchaseState.Deferred:
                _logs.Log($"Tienda: compra pendiente — {compra.ProductId}");
                return false;

            case PurchaseState.Failed:
                _logs.Log($"Tienda: compra fallida — {compra.ProductId}");
                // Flutter también envía errores al backend
                await EnviarAlBackendYCompletarAsync(compra, productoCatalogo, cfid, silencioso: true);
                return false;

            default:
                _logs.Log($"Tienda: compra cancelada/desconocida — state={compra.State}");
                return false;
        }
    }

    private async Task<bool> EnviarAlBackendYCompletarAsync(InAppBillingPurchase compra, DtoProducto productoCatalogo, Guid cfid, bool silencioso)
    {
        var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

#if IOS || MACCATALYST
        var pasarela = PasarelarPago.Apple;
        string? verificationData = null;
        try
        {
            var receiptUrl = Foundation.NSBundle.MainBundle.AppStoreReceiptUrl;
            var receiptData = receiptUrl != null ? Foundation.NSData.FromUrl(receiptUrl) : null;
            verificationData = receiptData?.GetBase64EncodedString(Foundation.NSDataBase64EncodingOptions.None);
        }
        catch { }
        verificationData ??= compra.OriginalJson;
        _logs.Log($"Tienda: receipt length={verificationData?.Length ?? 0}");
#elif ANDROID
        var pasarela = PasarelarPago.Google;
        var verificationData = compra.PurchaseToken;
#else
        var pasarela = PasarelarPago.Interbancario;
        var verificationData = compra.TransactionIdentifier;
#endif

        _logs.Log($"Tienda: verificando en backend — pasarela={pasarela} producto={compra.ProductId}");
        var verificado = await _servicioEcommerce.VerificarCompraIAP(
            cfid,
            dispositivoId,
            compra.ProductId,
            verificationData,
            compra.TransactionIdentifier,
            productoCatalogo,
            pasarela);

        _logs.Log($"Tienda: verificación backend — verificado={verificado}");

        // Consumir la compra (completePurchase) para permitir futuras compras del mismo producto
        await _servicioIAP.ConsumirCompraAsync(compra.ProductId, compra.PurchaseToken ?? compra.TransactionIdentifier);
        _logs.Log($"Tienda: compra consumida — {compra.ProductId}");

        // Actualizar licenciamiento en AppState (updateLicense)
        await _servicioSesion.GetLicenciaAsync();
        _logs.Log("Tienda: licencia actualizada");

        if (!silencioso)
        {
            if (verificado)
                await _servicioAlerta.MostrarAsync("¡Compra exitosa!", $"Tu {productoCatalogo.Nombre} ya está disponible en tu cuenta.", verBotonCancelar: false, confirmarText: "Aceptar");
            else
                await _servicioAlerta.MostrarAsync("Compra pendiente", "La compra se realizó pero no pudo verificarse de inmediato. Los créditos se acreditarán pronto.", verBotonCancelar: false, confirmarText: "Aceptar");
        }

        return verificado;
    }

    // ── Botón comprar ─────────────────────────────────────────────────────────

    private async void OnComprarClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not ProductoIAPModel modelo)
            return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null)
        {
            _logs.Log("Tienda: intento de compra sin cuenta fiscal seleccionada");
            await _servicioAlerta.MostrarAsync("Sin cuenta fiscal", "Selecciona una cuenta fiscal antes de comprar.", verBotonCancelar: false, confirmarText: "Aceptar");
            return;
        }

        var productoCatalogo = BuscarProductoEnCatalogo(modelo.Clave);
        if (productoCatalogo is null)
        {
            _logs.Log($"Tienda: producto no encontrado en catálogo — clave={modelo.Clave}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo identificar el producto. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "Aceptar");
            return;
        }

        _logs.Log($"Tienda: iniciando compra — producto={modelo.Clave} cuenta={cuenta.CuentaFiscalId}");
        SetCargando(true);
        try
        {
            var compra = await _servicioIAP.ComprarAsync(modelo.Clave);
            if (compra is null)
            {
                _logs.Log($"Tienda: compra cancelada por el usuario — producto={modelo.Clave}");
                return;
            }

            await ProcesarCompraAsync(compra, productoCatalogo, cuenta.CuentaFiscalId, silencioso: false);
        }
        catch (Exception ex) when (ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _logs.Log($"Tienda: compra cancelada por el usuario — producto={modelo.Clave}");
        }
        catch (Exception ex)
        {
            _logs.Log($"Tienda: excepción en compra — {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error durante la compra. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "Aceptar");
        }
        finally
        {
            SetCargando(false);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DtoProducto? BuscarProductoEnCatalogo(string iapId) =>
        _productosCreditos.FirstOrDefault(p =>
            iapId.EndsWith(p.Clave, StringComparison.OrdinalIgnoreCase));

    private string ObtenerUnidades(string iapId) =>
        BuscarProductoEnCatalogo(iapId)
            ?.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";

    private void SetCargando(bool cargando)
    {
        if (_loadingOverlay is not null)
            _loadingOverlay.IsVisible = cargando;
    }
}
