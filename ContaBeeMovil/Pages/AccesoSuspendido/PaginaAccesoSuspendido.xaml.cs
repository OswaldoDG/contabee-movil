using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.AccesoSuspendido;

public partial class PaginaAccesoSuspendido : ContentPage
{
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private bool _estaCargando;

    public PaginaAccesoSuspendido(IServicioSesion servicioSesion, IServicioToast toast)
    {
        InitializeComponent();
        _servicioSesion = servicioSesion;
        _toast = toast;
        BindingContext = this;
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        set
        {
            if (_estaCargando == value) return;
            _estaCargando = value;
            NotificarCarga();
        }
    }

    public bool NoEstaCargando => !_estaCargando;

    private void NotificarCarga()
    {
        OnPropertyChanged(nameof(EstaCargando));
        OnPropertyChanged(nameof(NoEstaCargando));
    }

    private async void OnReintentar(object sender, EventArgs e)
    {
        if (EstaCargando) return;
        EstaCargando = true;
        try
        {
            var reanudado = await _servicioSesion.IntentarReanudarLoginLessAsync();
            if (!reanudado)
                await _toast.MostrarAsync("Tu acceso sigue desactivado", ToastIcono.Warning, ToastPosicion.Bottom);
            // Si se reanudó, IntentarReanudarLoginLessAsync ya navegó al AppShell.
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async void OnVolverALogin(object sender, EventArgs e)
    {
        if (EstaCargando) return;
        EstaCargando = true;
        try
        {
            // El usuario abandona la sesión loginless: limpiamos el token guardado.
            await _servicioSesion.LimpiaTokensAsync();
            var paginaLogin = App.Services.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
