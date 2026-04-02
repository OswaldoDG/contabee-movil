using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.IAP;

namespace ContaBeeMovil.Pages.Tienda;

public partial class TiendaPage : ContentPage
{
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioIAP _servicioIAP;
    private readonly IServicioSesion _servicioSesion;

    private Grid? _loadingOverlay;
    private CollectionView? _listaProductos;
    private Border? _bannerProximamente;

    public TiendaPage(IServicioEcommerce servicioEcommerce, IServicioIAP servicioIAP, IServicioSesion servicioSesion)
    {
        InitializeComponent();
        _servicioEcommerce = servicioEcommerce;
        _servicioIAP       = servicioIAP;
        _servicioSesion    = servicioSesion;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _loadingOverlay    = this.FindByName<Grid>("LoadingOverlay");
        _listaProductos    = this.FindByName<CollectionView>("ListaProductos");
        _bannerProximamente = this.FindByName<Border>("BannerProximamente");

        await CargarProductosAsync();
    }

    private async Task CargarProductosAsync()
    {
        SetCargando(true);
        try
        {
            // 1. Obtener catálogo del backend y filtrar CREDITOS con precio > 0
            var resultado = await _servicioEcommerce.GetCatalogoProductos();
            if (!resultado.Ok || resultado.Payload is null)
            {
                await DisplayAlertAsync("Error", "No se pudo obtener el catálogo.", "Aceptar");
                return;
            }

            var categoriaCreditos = resultado.Payload
                .FirstOrDefault(c => c.Clave == "CREDITOS");

            if (categoriaCreditos?.Productos is null)
                return;

            var productosCreditos = categoriaCreditos.Productos
                .Where(p => p.Propiedades.Any(x => x.Propiedad == "credcaptura" && x.Valor == "true"))
                .Where(p => p.Precios.Any(pr => pr.Tipo == TipoPrecio.Publico && pr.Precio > 0))
                .OrderBy(p =>
                {
                    var prop = p.Propiedades.FirstOrDefault(x => x.Propiedad == "unidadesproducto");
                    return int.TryParse(prop?.Valor, out var u) ? u : 0;
                })
                .ToList();

            // 2. Extraer IDs para consultar al store (clave en minúsculas)
            var iapIds = productosCreditos.Select(p => $"contabee.creditos.{p.Clave.ToLower()}").ToArray();

            // 3. Consultar a la tienda (Apple/Google)
            var productosStore = (await _servicioIAP.ObtenerProductosAsync(iapIds)).ToList();
            var disponibleEnTienda = productosStore.Count > 0;

            // 4. Construir la lista de modelos para mostrar
            List<ProductoIAPModel> modelos;

            if (disponibleEnTienda)
            {
                modelos = productosStore.Select(sp => new ProductoIAPModel
                {
                    Clave              = sp.ProductId,
                    Nombre             = sp.Name,
                    Unidades           = ObtenerUnidades(productosCreditos, sp.ProductId),
                    PrecioTexto        = sp.LocalizedPrice,
                    PrecioValor        = sp.MicrosPrice / 1_000_000.0,
                    DisponibleEnTienda = true
                }).ToList();
            }
            else
            {
                // Mostrar catálogo con precios de referencia y botón deshabilitado
                modelos = productosCreditos.Select(p =>
                {
                    var precio = p.Precios.First(pr => pr.Tipo == TipoPrecio.Publico);
                    var unidades = p.Propiedades
                        .FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";
                    return new ProductoIAPModel
                    {
                        Clave              = p.Clave.ToLower(),
                        Nombre             = p.Nombre,
                        Unidades           = unidades,
                        PrecioTexto        = $"${precio.Precio:N2} MXN",
                        PrecioValor        = precio.Precio,
                        DisponibleEnTienda = false
                    };
                }).ToList();
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

    private static string ObtenerUnidades(IEnumerable<DtoProducto> productosCreditos, string iapId)
    {
        var producto = productosCreditos.FirstOrDefault(p =>
            p.Clave.Equals(iapId, StringComparison.OrdinalIgnoreCase));

        return producto?.Propiedades
            .FirstOrDefault(x => x.Propiedad == "unidadesproducto")?.Valor ?? "?";
    }

    private async void OnComprarClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not ProductoIAPModel modelo)
            return;

        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null)
        {
            await DisplayAlertAsync("Sin cuenta fiscal", "Selecciona una cuenta fiscal antes de comprar.", "Aceptar");
            return;
        }

        SetCargando(true);
        try
        {
            var compra = await _servicioIAP.ComprarAsync(modelo.Clave);
            if (compra is null)
            {
                await DisplayAlertAsync("Compra cancelada", "La compra fue cancelada o no se pudo completar.", "Aceptar");
                return;
            }

            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

#if IOS || MACCATALYST
            var pasarela = PasarelarPago.Apple;
#elif ANDROID
            var pasarela = PasarelarPago.Google;
#else
            var pasarela = PasarelarPago.Interbancario;
#endif

            var verificado = await _servicioEcommerce.VerificarCompraIAP(
                cuenta.CuentaFiscalId,
                dispositivoId,
                modelo.Clave,
                compra.TransactionIdentifier,
                pasarela);

            if (verificado)
                await DisplayAlertAsync("¡Compra exitosa!", $"Tu {modelo.Nombre} ya está disponible en tu cuenta.", "Aceptar");
            else
                await DisplayAlertAsync("Compra pendiente", "La compra se realizó pero no pudo verificarse de inmediato. Los créditos se acreditarán pronto.", "Aceptar");
        }
        catch (Exception ex) when (ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            // El usuario canceló el diálogo de compra — no mostrar error
        }
        catch
        {
            await DisplayAlertAsync("Error", "Ocurrió un error durante la compra. Intenta de nuevo.", "Aceptar");
        }
        finally
        {
            SetCargando(false);
        }
    }

    private void SetCargando(bool cargando)
    {
        if (_loadingOverlay is not null)
            _loadingOverlay.IsVisible = cargando;
    }
}
