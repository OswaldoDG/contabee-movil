using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Perfil;

public partial class MiCuentaPage : ContentPage
{
    public MiCuentaPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var noEsLoginLess = !AppState.Instance.EsLoginLess;
        CardCambiarContrasena.IsVisible = noEsLoginLess;
        CardEliminarCuenta.IsVisible    = noEsLoginLess;
    }

    private async void OnCambiarContrasenaClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CambiarContrasenaPage));
    }

    private async void OnAvisoPrivacidadClicked(object sender, TappedEventArgs e)
    {
        var visor = await VisorHtmlPage.DesdeArchivoAsync("Aviso de privacidad", "privacidad.html");
        await Navigation.PushModalAsync(visor);
    }

    private async void OnTerminosServicioClicked(object sender, TappedEventArgs e)
    {
        var visor = await VisorHtmlPage.DesdeArchivoAsync("Términos del servicio", "tos.html");
        await Navigation.PushModalAsync(visor);
    }

    private async void OnEliminarCuentaClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(EliminarCuentaPage));
    }
}
