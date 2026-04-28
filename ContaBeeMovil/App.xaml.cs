
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Registro;
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
            window = new Window(Services.GetRequiredService<AppShell>());
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
    public App(IServiceProvider services, DeviceService deviceService)
    {
        Services = services;
        _deviceService = deviceService;
        InitializeComponent();
    }

}
