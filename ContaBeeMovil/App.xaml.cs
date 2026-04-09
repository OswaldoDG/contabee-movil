
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services.Device;
namespace ContaBeeMovil;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; }
    private readonly DeviceService _deviceService;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = _deviceService.CheckDeviceIdAsync();
        var tieneSesion = Preferences.Get("TieneSesion", false);

        Window window;

        if (tieneSesion)
            window = new Window(Services.GetRequiredService<AppShell>());
        else
            window = new Window(Services.GetRequiredService<PaginaLogin>());

        // Notificar cuando la ventana esté lista
        window.Created += async (s, e) =>
        {
            await Task.Delay(800);
            DeepLinkHandler.NotifyAppReady();
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
