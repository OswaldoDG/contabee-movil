using ContaBeeMovil.Pages.Camara;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RegistrarRFCsPage : ContentPage
{
    public RegistrarRFCsPage()
    {
        InitializeComponent();
    }

    private async void IconManual_Tapped(object? sender, EventArgs e)
    {
        try
        {
            var page = MauiProgram.Services.GetService(typeof(ManualRegistroPage)) as Page;
            if (page != null)
                await Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo abrir el registro manual: {ex.Message}", "OK");
        }
    }

    private async void IconQr_Tapped(object? sender, EventArgs e)
    {
        try
        {
            var qrPage = MauiProgram.Services.GetService(typeof(QRPage)) as Page;
            if (qrPage != null)
                await Navigation.PushModalAsync(qrPage);
            else
                await DisplayAlert("Error", "No se pudo abrir el escaneo de QR.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo abrir el escaneo QR: {ex.Message}", "OK");
        }
    }

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        await DisplayAlert("Vincular", "Funcionalidad para vincular próximamente.", "OK");
    }
}
