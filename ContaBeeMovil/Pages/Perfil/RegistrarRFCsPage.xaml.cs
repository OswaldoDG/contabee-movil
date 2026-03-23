using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Services;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RegistrarRFCsPage : ContentPage
{
    private readonly IServicioAlerta _servicioAlerta;

    public RegistrarRFCsPage(IServicioAlerta servicioAlerta)
    {
        InitializeComponent();
        _servicioAlerta = servicioAlerta;
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
            await _servicioAlerta.MostrarAsync("Error", $"No se pudo abrir el registro manual: {ex.Message}", verBotonCancelar: false, confirmarText: "OK");
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
                await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo de QR.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            await _servicioAlerta.MostrarAsync("Error", $"No se pudo abrir el escaneo QR: {ex.Message}", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        await _servicioAlerta.MostrarAsync("Vincular", "Funcionalidad para vincular próximamente.", verBotonCancelar: false, confirmarText: "OK");
    }
}
