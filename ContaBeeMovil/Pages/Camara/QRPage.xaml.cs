using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.PageModels.Camara;
using ContaBeeMovil.Services.Device;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ContaBeeMovil.Pages.Camara;

public partial class QRPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private bool _isProcessing;
    private CancellationTokenSource? _cleanupCts;

    public QRPage(QRPageModel pageModel, IServicioCrm servicioCrm)
    {
        InitializeComponent();
        BindingContext = pageModel;
        _servicioCrm = servicioCrm;

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

        // Cancelar cualquier limpieza pendiente si el usuario volvió a abrir la página
        _cleanupCts?.Cancel();
        _isProcessing = false;

        // Re-añadir al árbol si fue removido en la limpieza anterior
        if (!CameraGrid.Children.Contains(BarcodeReader))
            CameraGrid.Children.Insert(0, BarcodeReader);

        BarcodeReader.IsVisible = true;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status == PermissionStatus.Granted)
            BarcodeReader.IsDetecting = true;
        else
            await DisplayAlert("Permiso requerido", "Se necesita acceso a la cámara para escanear QR.", "OK");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        BarcodeReader.IsVisible = false;

        // Retrasar la limpieza hasta DESPUÉS de que termine la animación de pop (~300ms)
        // Hacerlo durante la animación congela la navegación
        _cleanupCts?.Cancel();
        _cleanupCts = new CancellationTokenSource();
        var token = _cleanupCts.Token;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () =>
        {
            if (token.IsCancellationRequested) return;
            BarcodeReader.Handler?.DisconnectHandler();
            if (CameraGrid.Children.Contains(BarcodeReader))
                CameraGrid.Children.Remove(BarcodeReader);
        });
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
                await DisplayAlert("Error", ex.Message, "OK");
                _isProcessing = false;
                BarcodeReader.IsDetecting = true;
            }
        });
    }

    private async void BtnCancelar_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async Task ProcessQrResultAsync(string url)
    {
        var model = (QRPageModel)BindingContext;

        if (string.IsNullOrEmpty(model.TipoPersona))
        {
            await DisplayAlert("Atención", "Seleccione el tipo de persona.", "OK");
            _isProcessing = false;
            BarcodeReader.IsDetecting = true;
            return;
        }

        bool confirmar = await DisplayAlert(
            "QR detectado",
            $"URL: {url}\n\nTipo: {model.TipoPersona}\nCompartido: {(model.Compartido ? "Sí" : "No")}\n\n¿Desea continuar?",
            "Confirmar", "Cancelar");

        if (!confirmar)
        {
            _isProcessing = false;
            BarcodeReader.IsDetecting = true;
            return;
        }

        var tipo = model.TipoPersona.Equals("Moral", StringComparison.OrdinalIgnoreCase)
            ? TipoPersonaFiscal.Moral
            : TipoPersonaFiscal.Fisica;

        var request = new RequestUrl
        {
            Url = url,
            Compartido = model.Compartido,
            Tipo = tipo
        };

        LoadingOverlay.IsVisible = true;

        var respuesta = await _servicioCrm.EnviarUrlCuentaFiscal(request);

        if (respuesta.Ok)
        {
            var cuentas = await _servicioCrm.GetAsociacionesFiscales();
            if (cuentas.Ok && cuentas.Payload != null)
                AppState.Instance.CuentasFiscales = cuentas.Payload;

            LoadingOverlay.IsVisible = false;
            await Navigation.PopModalAsync();
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            LoadingOverlay.IsVisible = false;
            var mensaje = respuesta.Error?.Mensaje ?? "Error al registrar la cuenta fiscal.";
            await DisplayAlert("Error", mensaje, "OK");
            _isProcessing = false;
            BarcodeReader.IsDetecting = true;
        }
    }
}
