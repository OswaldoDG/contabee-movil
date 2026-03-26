namespace ContaBeeMovil.Pages.Perfil;

public partial class ConfiguracionPage : ContentPage
{
    public ConfiguracionPage()
    {
        InitializeComponent();
    }

    private async void OnCambiarContrasenaClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CambiarContrasenaPage));
    }

    private async void OnEliminarCuentaClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(EliminarCuentaPage));
    }
}
