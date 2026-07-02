
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Pages.SinConexion;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
namespace ContaBeeMovil;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }
    private readonly DeviceService _deviceService;

    protected override void OnStart()
    {
        base.OnStart();
        AppState.Instance.CargarDesdePreferencias();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppState.Instance.CargarDesdePreferencias();
        _ = Services.GetRequiredService<IServicioSesion>().VerificarSesionAlReanudarAsync();
#if IOS
        // Revisar App Group al reanudar — cubre el caso donde el usuario toca
        // la notificación "foto lista" que programa la Share Extension
        ContaBeeMovil.Helpers.SharedImageHandler.NotifySharedImageScheme();
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = _deviceService.CheckDeviceIdAsync();
        var tieneSesion = Preferences.Get("TieneSesion", false);

        Window window;

        if (tieneSesion)
        {
            var access = Connectivity.Current.NetworkAccess;
            if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
                AppState.Instance.ModoOffline = true;
            window = new Window(Services.GetRequiredService<AppShell>());
        }
        else if (Preferences.Get(ServicioSesion.CLAVE_TIENE_TOKEN_LOGINLESS, false))
        {
            // Usuario loginless cuya sesión quedó inactiva (acceso fiscal desactivado).
            // El token loginless sigue guardado, así que intentamos reanudar la sesión
            // automáticamente en vez de caer en PaginaLogin —donde un loginless no tiene
            // email/contraseña y quedaría atrapado. Mostramos PaginaCargando mientras
            // resolvemos.
            window = new Window(new Pages.PaginaCargando());
            _ = ReanudarLoginLessAlArrancarAsync();
        }
        else
            window = new Window(new NavigationPage(Services.GetRequiredService<PaginaLogin>()));

        // Notificar cuando la ventana esté lista
        window.Created += async (s, e) =>
        {
            await Task.Delay(800);
            DeepLinkHandler.NotifyAppReady();
            SharedImageHandler.NotifyAppReady();
        };

        return window;
    }
    // Arranque frío de un loginless con sesión inactiva: reintenta la sesión con el
    // token guardado. Si se logra (asociación reactivada o token aún válido) navega al
    // AppShell —posiblemente en modo limitado si sigue sin cuentas—. Si falla (token
    // revocado o cuenta inactivada) no hay sesión posible → login.
    private static async Task ReanudarLoginLessAlArrancarAsync()
    {
        var sesion = Services.GetRequiredService<IServicioSesion>();
        var reanudado = await sesion.IntentarReanudarLoginLessAsync();
        if (reanudado) return;

        // No se pudo reanudar. Si el token loginless SIGUE presente, el fallo fue por red
        // (offline/transitorio): mostramos "Sin conexión", que reintenta automáticamente al
        // volver la red. Si el token ya no está, fue revocado y el coordinador ya navegó a
        // Login. Nunca dejamos a un loginless atrapado en Login sin querer.
        bool tokenSigue = !string.IsNullOrEmpty(await sesion.LeeTokenLoginLessAsync());
        if (tokenSigue)
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current!.Windows[0].Page = Services.GetRequiredService<PaginaSinConexion>());
    }

    public App(IServiceProvider services, DeviceService deviceService)
    {
        Services = services;
        _deviceService = deviceService;
        InitializeComponent();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        // Precarga (warm-up) de las fuentes de íconos para evitar el "tofu" (rectángulo)
        // que se ve en el primer render mientras el typeface aún no está en caché.
        // MAUI cachea los typefaces por familia, así que crearlos en segundo plano
        // al arrancar deja la caché lista antes de dibujar la primera pantalla.
        Task.Run(PrecargarFuentesIconos);
    }

    private static void PrecargarFuentesIconos()
    {
        try
        {
            var fontManager = Services.GetService<IFontManager>();
            if (fontManager is null) return;

            // "MaterialIcons" = alias con el que MauiIcons.Material registra su fuente.
            // "FluentUI" = fuente de íconos propia de la app (Resources/Fonts).
            string[] familias = { "MaterialIcons", "FluentUI", "FluentUIFilled", "OpenSansRegular", "OpenSansSemibold" };
            foreach (var familia in familias)
            {
                var fuente = Microsoft.Maui.Font.OfSize(familia, 24);
                // El método para materializar el typeface tiene distinto nombre por plataforma.
#if ANDROID
                _ = fontManager.GetTypeface(fuente);
#elif IOS || MACCATALYST
                _ = fontManager.GetFont(fuente);
#elif WINDOWS
                _ = fontManager.GetFontFamily(fuente);
#endif
            }
        }
        catch
        {
            // Warm-up best-effort: si falla, los íconos siguen funcionando (solo sin precarga).
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        // Usar el estado actual, no el del evento (puede ser transitorio en Android)
        var access = Connectivity.Current.NetworkAccess;
        var tieneInternet = access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;

        if (tieneInternet)
        {
            AppState.Instance.ModoOffline = false;

            if (Preferences.Get("TarjetasPendienteSincronizacion", false))
                _ = Task.Delay(2000).ContinueWith(_ =>
                    Services.GetRequiredService<IServicioSesion>().GetTarjetasAsync());
        }
        else
        {
            AppState.Instance.ModoOffline = true;
        }
    }

}
