using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Perfil;

public partial class MiCuentaPage : ContentPage
{
    private readonly IServicioSesion _servicioSesion;

    public MiCuentaPage()
    {
        _servicioSesion = MauiProgram.Services.GetRequiredService<IServicioSesion>();
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var noEsLoginLess = !AppState.Instance.EsLoginLess;
        CardCambiarContrasena.IsVisible = noEsLoginLess;
        CardEliminarCuenta.IsVisible    = noEsLoginLess;
        DangerZone.IsVisible = noEsLoginLess;
        Divide.IsVisible = noEsLoginLess;

        _ = CargarDatosSesionAsync();
    }

    private async Task CargarDatosSesionAsync()
    {
        var correo = await _servicioSesion.LeeEmailAsync();
        var idDispositivo = await _servicioSesion.LeeIdDeDispositivo();

        LblCorreoSesion.Text = string.IsNullOrWhiteSpace(correo) ? "No disponible" : correo;
        LblIdDispositivo.Text = string.IsNullOrWhiteSpace(idDispositivo) ? "No disponible" : idDispositivo;
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
