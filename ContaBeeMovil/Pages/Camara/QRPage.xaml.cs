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
    private int _isProcessing;
    private int _cerrando;
    private CameraBarcodeReaderView? BarcodeReader;

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
    }

    /// <summary>
    /// El lector abre la cámara al conectarse su handler; si eso pasa antes de conceder el
    /// permiso falla y se queda en negro sin reintentar. Por eso se crea hasta tener permiso.
    /// </summary>
    private void CrearLector()
    {
        BarcodeReader = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.All,
                AutoRotate = true,
                Multiple = false
            }
        };
        BarcodeReader.BarcodesDetected += OnBarcodesDetected;
        CameraGrid.Insert(0, BarcodeReader); // debajo de la guía de encuadre
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Interlocked.Exchange(ref _isProcessing, 0);

        // Se re-evalúa en cada OnAppearing — también al volver de los ajustes del sistema,
        // así el escáner queda operativo sin tener que reiniciar la app.
        var tienePermiso = await _permisos.AsegurarCamaraAsync("para escanear el código QR");
        if (Volatile.Read(ref _cerrando) != 0 || Navigation.ModalStack.LastOrDefault() != this)
            return;

        if (tienePermiso)
        {
            if (BarcodeReader == null)
                CrearLector();
            BarcodeReader!.IsDetecting = true;
        }
        else if (Navigation.ModalStack.Contains(this))
        {
            // Sin permiso el visor queda en negro: se cierra en lugar de dejar al usuario atorado.
            await CerrarModalAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BarcodeReader == null) return;

        // No quitar el lector del Grid aquí: durante PopModalAsync el contexto del layout
        // puede estar desconectado y Grid.Remove provoca InvalidOperationException.
        BarcodeReader.IsDetecting = false;
        if (Volatile.Read(ref _cerrando) != 0)
        {
            BarcodeReader.BarcodesDetected -= OnBarcodesDetected;
            BarcodeReader.Handler?.DisconnectHandler();
        }
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (Volatile.Read(ref _cerrando) != 0) return;

        var result = e.Results?.FirstOrDefault();
        if (result is null || string.IsNullOrEmpty(result.Value)) return;

        // ZXing llama este evento desde otro hilo y puede entregar varios frames del
        // mismo QR. Solo uno debe intentar cerrar el modal.
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (BarcodeReader != null) BarcodeReader.IsDetecting = false;
                await ProcessQrResultAsync(result.Value);
            }
            catch (Exception ex)
            {
                _logs.Error($"[QRPage] {ex}");
                try
                {
                    await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error al procesar el código QR.", verBotonCancelar: false, confirmarText: "OK");
                }
                catch (Exception alertaEx)
                {
                    _logs.Error($"[QRPage] No se pudo mostrar el error: {alertaEx}");
                }
                finally
                {
                    ReactivarEscaner();
                }
            }
        });
    }

    private async void BtnCancelar_Clicked(object? sender, EventArgs e)
    {
        await CerrarModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CerrarModalAsync();
        return true;
    }

    private async Task<bool> CerrarModalAsync()
    {
        if (Interlocked.CompareExchange(ref _cerrando, 1, 0) != 0) return false;

        var cerrado = false;
        try
        {
            if (Navigation.ModalStack.LastOrDefault() != this) return false;
            await Navigation.PopModalAsync();
            cerrado = true;
            return true;
        }
        catch (Exception ex)
        {
            _logs.Error($"[QRPage] No se pudo cerrar el escáner: {ex}");
            return false;
        }
        finally
        {
            if (!cerrado) Interlocked.Exchange(ref _cerrando, 0);
        }
    }

    private const string DemoQrUrl = "https://siat.sat.gob.mx/app/qr/faces/pages/mobile/validadorqr.jsf?D1=10&D2=1&D3=00000000000_DEMO800101AA1";

    private async Task ProcessQrResultAsync(string url)
    {
        if (url.Equals(DemoQrUrl, StringComparison.OrdinalIgnoreCase))
        {
            if (!await CerrarModalAsync()) { ReactivarEscaner(); return; }
            if (AlRegistrarDemo != null)
                await AlRegistrarDemo();
            return;
        }

        // Cerrar la cámara de inmediato y delegar preview + popup + registro al llamador.
        if (!await CerrarModalAsync()) { ReactivarEscaner(); return; }
        if (AlObtenerUrl != null)
            await AlObtenerUrl(url);
    }

    private void ReactivarEscaner()
    {
        Interlocked.Exchange(ref _isProcessing, 0);
        if (Navigation.ModalStack.LastOrDefault() == this && BarcodeReader != null)
            BarcodeReader.IsDetecting = true;
    }
}
