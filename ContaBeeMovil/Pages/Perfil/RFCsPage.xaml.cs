using ContaBeeMovil.Helpers;
using Microsoft.Maui.Controls;
// using Contabee.Api.abstractions; // not required in this code-behind after UI separation
using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Pages.Camara;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RFCsPage : ContentPage
{
    public RFCsPage()
    {
        InitializeComponent();

        // Aplicar colores desde Resources usando el helper
        try
        {
            // HeaderFrame and HeaderTitle may not exist in the new layout; attempt safe update
            if (this.FindByName("HeaderFrame") is Microsoft.Maui.Controls.Frame hf)
                hf.BackgroundColor = UIHelpers.GetColor("Primary");
            if (this.FindByName("HeaderTitle") is Microsoft.Maui.Controls.Label ht)
                ht.TextColor = UIHelpers.GetColor("PrimaryText");
        }
        catch
        {
            // Ignore if resources not ready
        }
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
            await BtnQr_Clicked(sender, e);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo abrir el escaneo QR: {ex.Message}", "OK");
        }
    }

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        try
        {
            await BtnVincular_Clicked(sender, e);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }

    // Nota: la UI del formulario manual se mueve a "ManualRegistroPage.xaml". La lógica de interacción
    // se implementa en su code-behind (ManualRegistroPage.xaml.cs). Aquí solo se realiza la navegación.

    // Método eliminado: la navegación desde botones/gestos ahora abre ManualRegistroPage (XAML)

    private async Task BtnQr_Clicked(object? sender, EventArgs e)
    {
        var qrPage = MauiProgram.Services.GetService(typeof(Pages.Camara.QRPage)) as Page;
        if (qrPage != null)
            await Navigation.PushModalAsync(qrPage);
        else
            await DisplayAlert("Error", "No se pudo abrir el escaneo de QR.", "OK");
    }

    private async Task BtnVincular_Clicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Vincular", "Funcionalidad para vincular próximamente.", "OK");
    }
}
