using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.IAP;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Views;
using Plugin.InAppBilling;

namespace ContaBeeMovil.Pages.Tienda;

public partial class TiendaPage : ContentPage
{
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioIAP _servicioIAP;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioToast _toast;
    private readonly IServicioLogs _logs;

    private static readonly PopupOptions _popupOpts = new()
    {
        PageOverlayColor = Color.FromArgb("#66000000"),
        CanBeDismissedByTappingOutsideOfPopup = false,
    };

    private bool _cargado;

    private View? _loadingOverlay;
    private CollectionView? _listaProductos;
    private FlexLayout? _tabsCategorias;
    private VerticalStackLayout? _estadoVacio;
    private VerticalStackLayout? _debugCompraDirecta;
    private Label? _descripcionCategoria;

    private static readonly Dictionary<string, string> DescripcionesCategoria = new()
    {
        ["CREDITOS_CAPTURA"]      = "Captura tu ticket y ContaBee genera la factura automáticamente.",
        ["CREDITOS_COLABORACION"] = "Úsalos para crear comprobaciones y devoluciones.",
        ["CREDITOS_AUTOSERVICIO"] = "Captura tu ticket y genera tu factura tú mismo, desde nuestra app de escritorio.",
    };

    private List<DtoProducto> _todosLosProductos = [];
    private List<CategoriaTabModel> _categorias = [];
    private CategoriaTabModel? _categoriaActiva;

    private const string PrefsKeyComprasPendientes = "tienda.compras_pendientes";

    private static readonly (string Clave, string Nombre)[] CategoriasConfig =
    [
        ("CREDITOS_CAPTURA",       "Captura"),
        ("CREDITOS_COLABORACION",  "Colaboración"),
        ("CREDITOS_AUTOSERVICIO",  "Autoservicio"),
        ("REGALOS",                "Regalos"),
    ];

    public TiendaPage(IServicioEcommerce servicioEcommerce, IServicioIAP servicioIAP, IServicioSesion servicioSesion, IServicioAlerta servicioAlerta, IServicioToast toast, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioEcommerce = servicioEcommerce;
        _servicioIAP       = servicioIAP;
        _servicioSesion    = servicioSesion;
        _servicioAlerta    = servicioAlerta;
        _toast             = toast;
        _logs              = logs;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _loadingOverlay     = this.FindByName<View>("LoadingOverlay");
        _listaProductos     = this.FindByName<CollectionView>("ListaProductos");
        _tabsCategorias     = this.FindByName<FlexLayout>("TabsCategorias");
        _estadoVacio        = this.FindByName<VerticalStackLayout>("EstadoVacio");
        _debugCompraDirecta = this.FindByName<VerticalStackLayout>("DebugCompraDirecta");
        _descripcionCategoria = this.FindByName<Label>("DescripcionCategoria");

        if (_debugCompraDirecta is not null)
            _debugCompraDirecta.IsVisible = AppState.Instance.EsDev;

        if (_cargado) return;
        _cargado = true;

        _logs.Log("Tienda: página abierta");
        await CargarProductosAsync();
        await ReintentarComprasPendientesLocalesAsync();
        await RestaurarComprasPendientesAsync();
    }

    // ── Carga del catálogo ────────────────────────────────────────────────────

    private async Task CargarProductosAsync()
    {
        _logs.Log("Tienda: iniciando carga de catálogo");
        SetCargando(true);
        try
        {
            // Inicializar las 4 categorías vacías
            _categorias = CategoriasConfig
                .Select((c, i) => new CategoriaTabModel
                {
                    Clave          = c.Clave,
                    Nombre         = c.Nombre,
                    EsSeleccionada = i == 0,
                })
                .ToList();

            var resultado = await _servicioEcommerce.GetCatalogoProductos();
            if (!resultado.Ok || resultado.Payload is null)
            {
                _logs.Log($"Tienda: error al obtener catálogo — {resultado.Error?.Mensaje ?? "sin detalle"}");
                await _servicioAlerta.MostrarAsync("Error", "No se pudo obtener el catálogo.", verBotonCancelar: false, confirmarText: "Aceptar");
                ActualizarUI();
                return;
            }

            // ── Créditos por categoría (captura / colaboración / autoservicio) ──
            await CargarCategoriaCreditosAsync(resultado.Payload, "CREDITOS",      "credcaptura", "CREDITOS_CAPTURA",      "Captura",      "captura");
            await CargarCategoriaCreditosAsync(resultado.Payload, "CREDITOSCOLAB", "credcolab",   "CREDITOS_COLABORACION", "Colaboración", "colaboracion");
            await CargarCategoriaCreditosAsync(resultado.Payload, "CREDITOSAUTO",  "credauto",    "CREDITOS_AUTOSERVICIO", "Autoservicio", "autoservicio");

            ActualizarUI();
        }
        finally
        {
            SetCargando(false);
        }
    }

    private async Task CargarCategoriaCreditosAsync(
        ICollection<DtoCategoriasProducto> catalogo,
        string backendClave, string propiedadFiltro, string tabClave, string nombreTipo, string imagenCategoria)
    {
        var categoria = catalogo.FirstOrDefault(c => c.Clave == backendClave);
        if (categoria?.Productos is null) return;

        var productos = categoria.Productos
            .Where(p => p.Propiedades.Any(x => x.Propiedad == propiedadFiltro && x.Valor == "true"))
            .Where(p => p.Precios.Any(pr => pr.Tipo == TipoPrecio.Publico && pr.Precio > 0))
            .OrderBy(p =>
            {
                var prop = p.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto");
                return int.TryParse(prop?.Valor, out var u) ? u : 0;
            })
            .ToList();

        _todosLosProductos.AddRange(productos);
        _logs.Log($"Tienda: {productos.Count} productos {tabClave} encontrados");

        var iapIds = productos.Select(p => $"contabee.{backendClave.ToLower()}.{p.Clave.ToLower()}").ToArray();
        var productosStore = (await _servicioIAP.ObtenerProductosAsync(iapIds)).ToList();
        var disponibleEnTienda = productosStore.Count > 0;
        _logs.Log($"Tienda: store respondió {productosStore.Count} productos para {tabClave} — ids={string.Join(",", iapIds)} disponible={disponibleEnTienda}");

        List<ProductoIAPModel> modelos = disponibleEnTienda
            ? productosStore.Select(sp => new ProductoIAPModel
            {
                Clave              = sp.ProductId,
                Nombre             = sp.Name.Contains('(') ? sp.Name[..sp.Name.IndexOf('(')].Trim() : sp.Name,
                Unidades           = ObtenerUnidades(sp.ProductId),
                PrecioTexto        = sp.LocalizedPrice,
                PrecioValor        = sp.MicrosPrice / 1_000_000.0,
                DisponibleEnTienda = true,
                Imagen             = ImagenParaUnidades(ObtenerUnidades(sp.ProductId), imagenCategoria),
            })
            .OrderBy(m => m.PrecioValor)
            .ToList()
            : productos.Select(p =>
            {
                var precio   = p.Precios.First(pr => pr.Tipo == TipoPrecio.Publico);
                var unidades = p.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";
                return new ProductoIAPModel
                {
                    Clave              = $"contabee.{backendClave.ToLower()}.{p.Clave.ToLower()}",
                    Nombre             = $"Paquete {unidades} Créditos {nombreTipo}",
                    Unidades           = unidades,
                    PrecioTexto        = $"${precio.Precio:N2} MXN",
                    PrecioValor        = precio.Precio,
                    DisponibleEnTienda = false,
                    Imagen             = ImagenParaUnidades(unidades, imagenCategoria),
                };
            })
            .OrderBy(m => m.PrecioValor)
            .ToList();

        _categorias.First(c => c.Clave == tabClave).Productos = modelos;
    }

    private void ActualizarUI()
    {
        _categoriaActiva = _categorias.FirstOrDefault(c => c.EsSeleccionada) ?? _categorias.FirstOrDefault();

        if (_tabsCategorias is not null)
            BindableLayout.SetItemsSource(_tabsCategorias, _categorias);

        if (_categoriaActiva is not null)
            MostrarCategoria(_categoriaActiva);
    }

    // ── Selección de tab ──────────────────────────────────────────────────────

    private void OnCategoriaSeleccionada(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not CategoriaTabModel cat) return;

        foreach (var c in _categorias) c.EsSeleccionada = false;
        cat.EsSeleccionada = true;
        _categoriaActiva = cat;

        // INotifyPropertyChanged en CategoriaTabModel actualiza los triggers sin resetear el source

        MostrarCategoria(cat);
    }

    private void MostrarCategoria(CategoriaTabModel cat)
    {
        var tieneProductos = cat.Productos.Count > 0;

        if (_descripcionCategoria is not null)
        {
            DescripcionesCategoria.TryGetValue(cat.Clave, out var desc);
            _descripcionCategoria.Text      = desc ?? string.Empty;
            _descripcionCategoria.IsVisible = !string.IsNullOrEmpty(desc);
        }

        if (_listaProductos is not null)
        {
            _listaProductos.IsVisible   = tieneProductos;
            _listaProductos.ItemsSource = tieneProductos ? cat.Productos : null;
        }

        if (_estadoVacio is not null)
            _estadoVacio.IsVisible = !tieneProductos;
    }

    // ── Restaurar compras pendientes ──────────────────────────────────────────

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

    // ── Máquina de estados de compra ──────────────────────────────────────────

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
            var receiptPath = receiptUrl?.Path;
            _logs.Log($"Tienda: receipt path={receiptPath} exists={receiptPath != null && System.IO.File.Exists(receiptPath)}");
            if (receiptPath != null && System.IO.File.Exists(receiptPath))
            {
                var bytes = System.IO.File.ReadAllBytes(receiptPath);
                verificationData = Convert.ToBase64String(bytes);
                _logs.Log($"Tienda: receipt leído OK — bytes={bytes.Length} b64length={verificationData.Length} preview={verificationData[..Math.Min(60, verificationData.Length)]}...");
            }
        }
        catch (Exception exReceipt)
        {
            _logs.Log($"Tienda: error leyendo receipt — {exReceipt.Message}");
        }
        verificationData ??= compra.OriginalJson;
        _logs.Log($"Tienda: receipt length={verificationData?.Length ?? 0}");
#elif ANDROID
        var pasarela = PasarelarPago.Google;
        var verificationData = compra.PurchaseToken;
#else
        var pasarela = PasarelarPago.Interbancario;
        var verificationData = compra.TransactionIdentifier;
#endif

        var precioPublico = productoCatalogo.Precios.FirstOrDefault(p => p.Tipo == TipoPrecio.Publico);
        var comprobante = new DtoComprobanteCompra
        {
            CuentaFiscalId   = cfid.ToString(),
            DispositivoId    = dispositivoId,
            PasarelarPago    = pasarela,
            PasarelaId       = verificationData,
            CompraId         = compra.TransactionIdentifier,
            ProductoTiendaId = compra.ProductId,
            MontoCompra      = precioPublico?.Precio ?? 0,
            Elementos        =
            [
                new DtoElementoCompra
                {
                    Id         = productoCatalogo.Id.ToString(),
                    ProductoId = productoCatalogo.Clave,
                    TipoPrecio = TipoPrecio.Publico,
                    Cantidad   = 1,
                    Periodo    = precioPublico?.PeriodoRenta ?? 1,
                }
            ],
        };

        _logs.Log($"Tienda: PAYLOAD verificar = {System.Text.Json.JsonSerializer.Serialize(comprobante)}");
        _logs.Log($"Tienda: verificando en backend — pasarela={pasarela} producto={compra.ProductId}");
        var verificado = await _servicioEcommerce.VerificarCompraIAP(cfid, comprobante);

        _logs.Log($"Tienda: verificación backend — verificado={verificado}");

        bool completado = false;
        if (verificado)
        {
            completado = await _servicioEcommerce.CompletarCompraIAP(cfid, comprobante);
            _logs.Log($"Tienda: completar backend — completado={completado}");
        }

        var tiposResaltar = new List<TipoCreditoResaltar>();

        if (completado)
        {
            await _servicioIAP.ConsumirCompraAsync(compra.ProductId, compra.PurchaseToken ?? compra.TransactionIdentifier);
            _logs.Log($"Tienda: compra consumida — {compra.ProductId}");

            // Snapshot de créditos antes del refresh para detectar qué tipo(s) aumentaron.
            var licAntes = AppState.Instance.Licenciamiento;
            int capAntes   = licAntes?.CreditosDisponibles      ?? 0;
            int autoAntes  = licAntes?.CreditosAutoDisponibles  ?? 0;
            int colabAntes = licAntes?.CreditosColabDisponibles ?? 0;

            await _servicioSesion.GetLicenciaAsync();
            _logs.Log("Tienda: licencia actualizada");

            var licDespues = AppState.Instance.Licenciamiento;
            if ((licDespues?.CreditosDisponibles      ?? 0) > capAntes)   tiposResaltar.Add(TipoCreditoResaltar.Captura);
            if ((licDespues?.CreditosAutoDisponibles  ?? 0) > autoAntes)  tiposResaltar.Add(TipoCreditoResaltar.Autoservicio);
            if ((licDespues?.CreditosColabDisponibles ?? 0) > colabAntes) tiposResaltar.Add(TipoCreditoResaltar.Colaboracion);
        }
        else
        {
            _logs.Log($"Tienda: compra NO consumida — guardando localmente — verificado={verificado}");
            GuardarCompraPendienteLocal(comprobante, compra.PurchaseToken ?? compra.TransactionIdentifier, productoCatalogo.Nombre);
        }

        if (!silencioso)
        {
            if (completado)
            {
                await _toast.MostrarAsync("¡Compra exitosa!", ToastIcono.Info, ToastPosicion.Bottom);
                DashboardPage.PendienteActualizar = true;   // recarga estadísticas del dashboard
                await Shell.Current.GoToAsync("..");        // vuelve de Tienda al MainTabbedPage
                if (tiposResaltar.Count > 0)
                    MainTabbedPage.SolicitarResaltarCreditos(tiposResaltar.ToArray());
            }
            else
            {
                await _servicioAlerta.MostrarAsync("Compra pendiente", "La compra se realizó pero no pudo verificarse de inmediato. Los créditos se acreditarán pronto.", verBotonCancelar: false, confirmarText: "Aceptar");
            }
        }

        return completado;
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
                await _toast.MostrarAsync("Compra cancelada", ToastIcono.Warning);
                return;
            }

            await ProcesarCompraAsync(compra, productoCatalogo, cuenta.CuentaFiscalId, silencioso: false);
        }
        catch (Exception ex) when (ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _logs.Log($"Tienda: compra cancelada por el usuario — producto={modelo.Clave}");
            await _toast.MostrarAsync("Compra cancelada", ToastIcono.Warning);
        }
        catch (Exception ex)
        {
            _logs.Log($"Tienda: excepción en compra — {ex.GetType().Name}: {ex.Message}");
            await _toast.MostrarAsync("La compra no se completó.", ToastIcono.Error);
        }
        finally
        {
            SetCargando(false);
        }
    }

    // ── Compra directa (debug) ────────────────────────────────────────────────

    private async void OnCompraDirectaClicked(object sender, EventArgs e)
    {
        const string productoId = "contabee.creditos.captura15";

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null)
        {
            await _servicioAlerta.MostrarAsync("Sin cuenta fiscal", "Selecciona una cuenta fiscal antes de comprar.", verBotonCancelar: false, confirmarText: "Aceptar");
            return;
        }

        _logs.Log($"Tienda: compra directa — iniciando IAP para {productoId}");
        SetCargando(true);
        try
        {
            var compra = await _servicioIAP.ComprarAsync(productoId);
            if (compra is null)
            {
                _logs.Log("Tienda: compra directa — cancelada por el usuario");
                await _toast.MostrarAsync("Compra cancelada", ToastIcono.Warning);
                return;
            }

            await ProcesarCompraDirectaAsync(compra, cuenta.CuentaFiscalId);
        }
        catch (Exception ex) when (ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            _logs.Log($"Tienda: compra directa — cancelada ({ex.Message})");
            await _toast.MostrarAsync("Compra cancelada", ToastIcono.Warning);
        }
        catch (Exception ex)
        {
            _logs.Log($"Tienda: compra directa — excepción {ex.GetType().Name}: {ex.Message}");
            await _toast.MostrarAsync("La compra no se completó.", ToastIcono.Error);
        }
        finally
        {
            SetCargando(false);
        }
    }

    private async Task ProcesarCompraDirectaAsync(InAppBillingPurchase compra, Guid cfid)
    {
        var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

#if IOS || MACCATALYST
        var pasarela = PasarelarPago.Apple;
        string? verificationData = null;
        try
        {
            var receiptUrl = Foundation.NSBundle.MainBundle.AppStoreReceiptUrl;
            var receiptData = receiptUrl != null ? Foundation.NSData.FromUrl(receiptUrl) : null;
            if (receiptData != null && receiptData.Length > 0)
                verificationData = receiptData.GetBase64EncodedString(Foundation.NSDataBase64EncodingOptions.None);
        }
        catch { }
        _logs.Log($"Tienda: receipt length={verificationData?.Length ?? 0}");
        if (string.IsNullOrEmpty(verificationData))
        {
            _logs.Log("Tienda: receipt no disponible — abortando verificación");
            await _servicioAlerta.MostrarAsync("Sin receipt", "No se pudo obtener el comprobante de Apple. Intenta desde TestFlight.", verBotonCancelar: false, confirmarText: "Aceptar");
            return;
        }
#elif ANDROID
        var pasarela = PasarelarPago.Google;
        var verificationData = compra.PurchaseToken;
#else
        var pasarela = PasarelarPago.Interbancario;
        var verificationData = compra.TransactionIdentifier;
#endif

        var comprobante = new DtoComprobanteCompra
        {
            CuentaFiscalId   = cfid.ToString(),
            DispositivoId    = dispositivoId,
            PasarelarPago    = pasarela,
            PasarelaId       = verificationData,
            CompraId         = compra.TransactionIdentifier,
            ProductoTiendaId = compra.ProductId,
            MontoCompra      = 0,
            Elementos        =
            [
                new DtoElementoCompra
                {
                    Id         = "captura15",
                    ProductoId = "CAPTURA15",
                    TipoPrecio = TipoPrecio.Publico,
                    Cantidad   = 1,
                    Periodo    = 1,
                }
            ],
        };

        _logs.Log($"Tienda: compra directa — PAYLOAD verificar = {System.Text.Json.JsonSerializer.Serialize(comprobante)}");
        var verificado = await _servicioEcommerce.VerificarCompraIAP(cfid, comprobante);
        _logs.Log($"Tienda: compra directa — verificado={verificado}");

        bool completado = false;
        if (verificado)
        {
            completado = await _servicioEcommerce.CompletarCompraIAP(cfid, comprobante);
            _logs.Log($"Tienda: compra directa — completado={completado}");
        }

        if (completado)
        {
            await _servicioIAP.ConsumirCompraAsync(compra.ProductId, compra.PurchaseToken ?? compra.TransactionIdentifier);
            _logs.Log("Tienda: compra directa — consumida");

            await _servicioSesion.GetLicenciaAsync();
            _logs.Log("Tienda: compra directa — licencia actualizada");
        }
        else
        {
            _logs.Log($"Tienda: compra directa NO consumida — guardando localmente — verificado={verificado}");
            GuardarCompraPendienteLocal(comprobante, compra.PurchaseToken ?? compra.TransactionIdentifier, "captura15");
        }

        if (completado)
            await _servicioAlerta.MostrarAsync("¡Compra exitosa!", "Los créditos captura15 ya están disponibles.", verBotonCancelar: false, confirmarText: "Aceptar");
        else
            await _servicioAlerta.MostrarAsync("Compra pendiente", "La compra se realizó pero no pudo verificarse de inmediato. Los créditos se acreditarán pronto.", verBotonCancelar: false, confirmarText: "Aceptar");
    }

    // ── Persistencia local de compras pendientes ──────────────────────────────

    private void GuardarCompraPendienteLocal(DtoComprobanteCompra comprobante, string purchaseToken, string nombreProducto)
    {
        try
        {
            var pendientes = LeerComprasPendientesLocales();
            if (pendientes.Any(p => p.Comprobante.CompraId == comprobante.CompraId))
            {
                _logs.Log($"Tienda: compra ya estaba en pendientes locales — {comprobante.ProductoTiendaId}");
                return;
            }
            pendientes.Add(new CompraPendienteLocal { Comprobante = comprobante, PurchaseToken = purchaseToken, NombreProducto = nombreProducto });
            Preferences.Default.Set(PrefsKeyComprasPendientes, System.Text.Json.JsonSerializer.Serialize(pendientes));
            _logs.Log($"Tienda: compra pendiente guardada localmente — {comprobante.ProductoTiendaId}");
        }
        catch (Exception ex)
        {
            _logs.Log($"Tienda: error guardando compra pendiente local — {ex.Message}");
        }
    }

    private static List<CompraPendienteLocal> LeerComprasPendientesLocales()
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

    private async Task ReintentarComprasPendientesLocalesAsync()
    {
        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null) return;

        var pendientes = LeerComprasPendientesLocales();
        if (pendientes.Count == 0) return;

        _logs.Log($"Tienda: {pendientes.Count} compras pendientes locales — reintentando");
        var remanentes = new List<CompraPendienteLocal>();

        foreach (var p in pendientes)
        {
            if (!Guid.TryParse(p.Comprobante.CuentaFiscalId, out var cfid))
                cfid = cuenta.CuentaFiscalId;

            var verificado = await _servicioEcommerce.VerificarCompraIAP(cfid, p.Comprobante);
            bool completado = false;
            if (verificado)
                completado = await _servicioEcommerce.CompletarCompraIAP(cfid, p.Comprobante);

            _logs.Log($"Tienda: reintento local — producto={p.Comprobante.ProductoTiendaId} verificado={verificado} completado={completado}");

            if (completado)
            {
                await _servicioIAP.ConsumirCompraAsync(p.Comprobante.ProductoTiendaId, p.PurchaseToken);
                await _servicioSesion.GetLicenciaAsync();
                _logs.Log($"Tienda: reintento local exitoso — {p.Comprobante.ProductoTiendaId}");
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

    private async void OnVerDetalleLicenciaClicked(object sender, EventArgs e)
    {
        await this.ShowPopupAsync(new ResumenLicenciaPopup(), _popupOpts, CancellationToken.None);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DtoProducto? BuscarProductoEnCatalogo(string iapId) =>
        _todosLosProductos.FirstOrDefault(p =>
            iapId.EndsWith(p.Clave, StringComparison.OrdinalIgnoreCase));

    private static readonly Dictionary<string, HashSet<string>> _unidadesConImagen = new()
    {
        ["captura"]      = ["15", "30", "50", "100", "250", "500"],
        ["colaboracion"] = ["10", "50", "100", "250", "500"],
        ["autoservicio"] = ["15", "30", "50", "100", "250", "500"],
    };

    private static readonly Dictionary<string, string> _imagenFallback = new()
    {
        ["captura"]      = "captura15.jpeg",
        ["colaboracion"] = "colaboracion10.jpeg",
        ["autoservicio"] = "autoservicio15.jpeg",
    };

    private static string ImagenParaUnidades(string? unidades, string imagenCategoria)
    {
        if (!string.IsNullOrEmpty(unidades) &&
            _unidadesConImagen.TryGetValue(imagenCategoria, out var validos) &&
            validos.Contains(unidades))
            return $"{imagenCategoria}{unidades}.jpeg";
        return _imagenFallback.TryGetValue(imagenCategoria, out var fallback) ? fallback : "captura15.jpeg";
    }

    private string ObtenerUnidades(string iapId) =>
        BuscarProductoEnCatalogo(iapId)
            ?.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";

    private void SetCargando(bool cargando)
    {
        if (_loadingOverlay is not null)
            _loadingOverlay.IsVisible = cargando;
    }

    private sealed class CompraPendienteLocal
    {
        public DtoComprobanteCompra Comprobante { get; set; } = null!;
        public string PurchaseToken { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
    }
}
