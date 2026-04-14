using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RegistrarRFCsPage : ContentPage
{
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;

    public RegistrarRFCsPage(IServicioAlerta servicioAlerta, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioAlerta = servicioAlerta;
        _logs = logs;
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
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el registro manual.", verBotonCancelar: false, confirmarText: "OK");
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
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo QR.", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        await _servicioAlerta.MostrarAsync("Vincular", "Funcionalidad para vincular próximamente.", verBotonCancelar: false, confirmarText: "OK");
    }
}
