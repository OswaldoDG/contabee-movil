using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ContaBeeMovil.Pages.Camara;

public partial class QRPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private bool _isProcessing;

    /// <summary>
    /// Invocado en cuanto se detecta la URL del QR. El llamador (RegistrarRFCsPage) hace el preview, muestra el popup y registra.
    /// </summary>
    public Func<string, Task>? AlObtenerUrl { get; set; }

    public QRPage(IServicioCrm servicioCrm, IServicioAlerta servicioAlerta, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _servicioAlerta = servicioAlerta;
        _logs = logs;

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isProcessing = false;
        BarcodeReader.IsVisible = true;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status == PermissionStatus.Granted)
            BarcodeReader.IsDetecting = true;
        else
            await _servicioAlerta.MostrarAsync("Permiso requerido", "Se necesita acceso a la cámara para escanear QR.", verBotonCancelar: false, confirmarText: "OK");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        BarcodeReader.Handler?.DisconnectHandler();
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;

        var result = e.Results?.FirstOrDefault();
        if (result is null || string.IsNullOrEmpty(result.Value)) return;

        _isProcessing = true;
        BarcodeReader.IsDetecting = false;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await ProcessQrResultAsync(result.Value);
            }
            catch (Exception ex)
            {
                _logs.Log($"[QRPage] {ex.GetType().Name}: {ex.Message}");
                await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error al procesar el código QR.", verBotonCancelar: false, confirmarText: "OK");
                _isProcessing = false;
                BarcodeReader.IsDetecting = true;
            }
        });
    }

    private async void BtnCancelar_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private const string DemoQrUrl = "https://siat.sat.gob.mx/app/qr/faces/pages/mobile/validadorqr.jsf?D1=10&D2=1&D3=00000000000_DEMO800101AA1";

    private async Task ProcessQrResultAsync(string url)
    {
        if (url.Equals(DemoQrUrl, StringComparison.OrdinalIgnoreCase))
        {
            await RegistrarRfcDemoAsync();
            return;
        }

        // Cerrar la cámara de inmediato y delegar preview + popup + registro al llamador.
        await Navigation.PopModalAsync();
        if (AlObtenerUrl != null)
            await AlObtenerUrl(url);
    }

    private async Task RegistrarRfcDemoAsync()
    {
        LoadingOverlay.IsVisible = true;

        var modelo = new CuentaFiscalMinima
        {
            Tipo               = TipoPersonaFiscal.Fisica,
            Rfc                = "DEMO800101AA1",
            Nombre             = "Empresa Demo ContaBee",
            CodigoPostal       = "01000",
            Compartido         = false,
            ClaveRegimenFiscal = "612"
        };

        var respuesta = await _servicioCrm.RegistrarCuentaFiscalMinima(modelo);

        if (respuesta.Ok)
        {
            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
            {
                AppState.Instance.CuentasFiscales = cuentas.Payload;
                AppState.Instance.CuentaFiscalActual ??= cuentas.Payload.FirstOrDefault();
            }

            LoadingOverlay.IsVisible = false;
            await Navigation.PopModalAsync();

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
            var mensaje = respuesta.Error?.Mensaje ?? "Error al registrar la cuenta demo.";
            await _servicioAlerta.MostrarAsync("Error", mensaje, verBotonCancelar: false, confirmarText: "OK");
            _isProcessing = false;
            BarcodeReader.IsDetecting = true;
        }
    }

    private void ReactivarEscaner()
    {
        _isProcessing = false;
        BarcodeReader.IsDetecting = true;
    }
}
