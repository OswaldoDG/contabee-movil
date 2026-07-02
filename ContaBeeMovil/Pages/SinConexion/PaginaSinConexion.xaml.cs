using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services;

namespace ContaBeeMovil.Pages.SinConexion;

public partial class PaginaSinConexion : ContentPage
{
    private readonly IServicioSesion _servicioSesion;
    private bool _reintentando;

    public PaginaSinConexion(IServicioSesion servicioSesion)
    {
        InitializeComponent();
        _servicioSesion = servicioSesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Auto-recuperación: reintentar en cuanto vuelva la conexión.
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var access = Connectivity.Current.NetworkAccess;
        if (access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet)
            MainThread.BeginInvokeOnMainThread(() => _ = ReintentarAsync());
    }

    private async void OnReintentar(object sender, EventArgs e) => await ReintentarAsync();

    private async Task ReintentarAsync()
    {
        if (_reintentando) return;

        var access = Connectivity.Current.NetworkAccess;
        if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
            return; // sigue sin red

        _reintentando = true;
        try
        {
            // Loginless con sesión expirada: reintentar la reanudación con el token guardado.
            // IntentarReanudar navega al AppShell si tiene éxito (o a Login si el token fue
            // revocado). Si falla por red/servidor, seguimos en esta pantalla y reintentamos
            // al próximo cambio de conectividad.
            if (!string.IsNullOrEmpty(await _servicioSesion.LeeTokenLoginLessAsync()))
            {
                await _servicioSesion.IntentarReanudarLoginLessAsync();
                return;
            }

            // Sesión normal → AppShell; sin sesión → Login.
            if (Preferences.Get("TieneSesion", false))
                Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
            else
                Application.Current!.Windows[0].Page = new NavigationPage(App.Services.GetRequiredService<PaginaLogin>());
        }
        finally
        {
            _reintentando = false;
        }
    }
}
