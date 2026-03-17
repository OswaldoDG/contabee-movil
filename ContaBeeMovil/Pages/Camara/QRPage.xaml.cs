using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.PageModels.Camara;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ContaBeeMovil.Pages.Camara;

public partial class QRPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private bool _isProcessing;

    public QRPage(QRPageModel pageModel, IServicioCrm servicioCrm)
    {
        InitializeComponent();
        BindingContext = pageModel;
        _servicioCrm = servicioCrm;

        try
        {
            this.BackgroundColor = UIHelpers.GetColor("Background");
            LabelTipoPersona.TextColor = UIHelpers.GetColor("PrimaryText");
            LabelCompartido.TextColor = UIHelpers.GetColor("PrimaryText");
        }
        catch { }

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };

        BtnCancelar.Clicked += async (_, __) => await Navigation.PopModalAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isProcessing = false;

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

        var respuesta = await _servicioCrm.EnviarUrlCuentaFiscal(request);

        if (respuesta.Ok)
        {
            await DisplayAlert("Éxito", "Cuenta fiscal registrada correctamente.", "OK");
            await Navigation.PopModalAsync();
        }
        else
        {
            var mensaje = respuesta.Error?.Mensaje ?? "Error al registrar la cuenta fiscal.";
            await DisplayAlert("Error", mensaje, "OK");
            _isProcessing = false;
            BarcodeReader.IsDetecting = true;
        }
    }
}
