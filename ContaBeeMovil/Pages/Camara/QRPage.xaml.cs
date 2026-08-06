using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Permisos;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ContaBeeMovil.Pages.Camara;

public partial class QRPage : ContentPage
{
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioPermisos _permisos;
    private readonly IServicioLogs _logs;
    private bool _isProcessing;

    /// <summary>
    /// Invocado en cuanto se detecta la URL del QR. El llamador (RegistrarRFCsPage) hace el preview, muestra el popup y registra.
    /// </summary>
    public Func<string, Task>? AlObtenerUrl { get; set; }

    /// <summary>
    /// Invocado cuando se detecta el QR demo. El llamador maneja preview, registro y feedback.
    /// </summary>
    public Func<Task>? AlRegistrarDemo { get; set; }

    public QRPage(IServicioAlerta servicioAlerta, IServicioPermisos permisos, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioAlerta = servicioAlerta;
        _permisos = permisos;
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

        // Se re-evalúa en cada OnAppearing — también al volver de los ajustes del sistema,
        // así el escáner queda operativo sin tener que reiniciar la app.
        if (await _permisos.AsegurarCamaraAsync("para escanear el código QR"))
        {
            BarcodeReader.IsDetecting = true;
        }
        else if (Navigation.ModalStack.Contains(this))
        {
            // Sin permiso el visor queda en negro: se cierra en lugar de dejar al usuario atorado.
            await Navigation.PopModalAsync();
        }
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
            await Navigation.PopModalAsync();
            if (AlRegistrarDemo != null)
                await AlRegistrarDemo();
            return;
        }

        // Cerrar la cámara de inmediato y delegar preview + popup + registro al llamador.
        await Navigation.PopModalAsync();
        if (AlObtenerUrl != null)
            await AlObtenerUrl(url);
    }

    private void ReactivarEscaner()
    {
        _isProcessing = false;
        BarcodeReader.IsDetecting = true;
    }
}
