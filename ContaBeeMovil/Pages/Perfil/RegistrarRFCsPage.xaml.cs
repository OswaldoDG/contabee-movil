using CommunityToolkit.Maui.Extensions;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Perfil;

[QueryProperty(nameof(FromLogin), "fromLogin")]
public partial class RegistrarRFCsPage : ContentPage
{
    public bool FromLogin
    {
        set => BtnVolverInicio.IsVisible = value;
    }

    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private readonly IServicioSesion _servicioSesion;

    public RegistrarRFCsPage(IServicioCrm servicioCrm, IServicioAlerta servicioAlerta, IServicioLogs logs, IServicioSesion servicioSesion)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _servicioAlerta = servicioAlerta;
        _logs = logs;
        _servicioSesion = servicioSesion;
    }

    private async void IconManual_Tapped(object? sender, EventArgs e)
    {
        try
        {
            var page = MauiProgram.Services.GetService(typeof(ManualRegistroPage)) as ManualRegistroPage;
            if (page != null)
            {
                page.ConfigurarAlta();
                await Navigation.PushAsync(page);
            }
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
            var qrPage = MauiProgram.Services.GetService(typeof(QRPage)) as QRPage;
            if (qrPage != null)
            {
                qrPage.AlObtenerUrl = async (url) => await MostrarPreviewYRegistrar(url);
                await Navigation.PushModalAsync(qrPage);
            }
            else
                await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo de QR.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo QR.", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async Task MostrarPreviewYRegistrar(string url)
    {
        // Spinner mientras llama al preview (cámara ya cerrada).
        LoadingOverlay.IsVisible = true;
        var preview = await _servicioCrm.PreviewUrlCuentaFiscal(new RequestUrlInfo { Url = url });
        LoadingOverlay.IsVisible = false;

        if (!preview.Ok || preview.Payload is null)
        {
            await _servicioAlerta.MostrarAsync("Error", MensajeErrorPreview(preview.Error), verBotonCancelar: false, confirmarText: "OK");
            return;
        }

        var popup = new PreviewCuentaFiscalPopup(preview.Payload);
        await this.ShowPopupAsync(popup);

        if (!popup.Confirmado) return;

        LoadingOverlay.IsVisible = true;

        var request = new RequestUrl
        {
            Url = url,
            Compartido = popup.Compartido,
            Tipo = popup.TipoSeleccionado
        };

        var respuesta = await _servicioCrm.EnviarUrlCuentaFiscal(request);

        if (respuesta.Ok)
        {
            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
            {
                AppState.Instance.CuentasFiscales = cuentas.Payload;
                AppState.Instance.CuentaFiscalActual ??= cuentas.Payload.FirstOrDefault();
            }

            LoadingOverlay.IsVisible = false;

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
            else
            {
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
        }
        else
        {
            LoadingOverlay.IsVisible = false;
            var mensaje = respuesta.Error?.Mensaje ?? "Error al registrar la cuenta fiscal.";
            await _servicioAlerta.MostrarAsync("Error", mensaje, verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private static string MensajeErrorPreview(Contabee.Api.ErrorProceso? error) => error?.HttpCode switch
    {
        System.Net.HttpStatusCode.BadRequest => "La URL del QR no es válida o no corresponde a una constancia.",
        System.Net.HttpStatusCode.TooManyRequests => "Demasiados intentos, espera un momento e inténtalo de nuevo.",
        System.Net.HttpStatusCode.Unauthorized => "Tu sesión expiró, inicia sesión nuevamente.",
        System.Net.HttpStatusCode.InternalServerError => "No se pudo leer la información de la constancia, intenta de nuevo.",
        _ => error?.Mensaje ?? "No se pudo obtener la información de la constancia."
    };

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        await _servicioAlerta.MostrarAsync("Vincular", "Funcionalidad para vincular próximamente.", verBotonCancelar: false, confirmarText: "OK");
    }

    private async void VolverAInicio_Clicked(object? sender, EventArgs e)
    {
        await _servicioSesion.CerrarSesionAsync();
    }
}
