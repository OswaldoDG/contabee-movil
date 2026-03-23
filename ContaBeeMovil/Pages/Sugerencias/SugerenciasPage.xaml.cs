using Contabee.Api;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Sugerencias;

public partial class SugerenciasPage : ContentPage
{
    private readonly IToastService _toastService;
    private readonly IServicioCrm _servicioCrm;

    public SugerenciasPage(IToastService toastService, IServicioCrm servicioCrm)
    {
        InitializeComponent();
        _toastService = toastService;
        _servicioCrm = servicioCrm;
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        MeGustaCounter.Text = $"{MeGustaEditor.Text?.Length ?? 0}/500";
        NoMeGustaCounter.Text = $"{NoMeGustaEditor.Text?.Length ?? 0}/500";
        MeGustariaCounter.Text = $"{MeGustariaEditor.Text?.Length ?? 0}/500";

        var tieneTexto = !string.IsNullOrWhiteSpace(MeGustaEditor.Text)
                      || !string.IsNullOrWhiteSpace(NoMeGustaEditor.Text)
                      || !string.IsNullOrWhiteSpace(MeGustariaEditor.Text);

        BtnEnviar.IsEnabled = tieneTexto;
    }

    private void MostrarLoader(bool visible)
    {
        Loader.IsRunning = visible;
        Loader.Opacity = visible ? 1 : 0;
    }

    private async void OnEnviarClicked(object? sender, EventArgs e)
    {
        BtnEnviar.IsEnabled = false;
        MostrarLoader(true);

        try
        {
            var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId.ToString() ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"[Sugerencias] CuentaFiscalId: '{cuentaFiscalId}'");

            var request = new FeedbackRequest
            {
                CuentaFiscalId = cuentaFiscalId,
                Elementos = new List<FeedbackElemento>
                {
                    new() { Tipo = "MeGusta",    Detalle = MeGustaEditor.Text?.Trim() ?? string.Empty },
                    new() { Tipo = "NoMeGusta",  Detalle = NoMeGustaEditor.Text?.Trim() ?? string.Empty },
                    new() { Tipo = "MeGustaria", Detalle = MeGustariaEditor.Text?.Trim() ?? string.Empty }
                }
            };

            System.Diagnostics.Debug.WriteLine($"[Sugerencias] Enviando feedback...");
            var resultado = await _servicioCrm.EnviarFeedback(request);
            System.Diagnostics.Debug.WriteLine($"[Sugerencias] Resultado: Ok={resultado.Ok}, HttpCode={resultado.HttpCode}, Error={resultado.Error?.Mensaje}, Origen={resultado.Error?.Origen}");

            MostrarLoader(false);

            if (resultado.Ok)
            {
                await _toastService.ShowAsync("Sugerencia enviada correctamente.", ToastType.Success, position: ToastPosition.Bottom);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var mensaje = resultado.Error?.Mensaje ?? "Error al enviar la sugerencia.";
                await _toastService.ShowAsync(mensaje, ToastType.Error, position: ToastPosition.Bottom);
                BtnEnviar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MostrarLoader(false);
            System.Diagnostics.Debug.WriteLine($"[Sugerencias] EXCEPCIÓN: {ex}");
            await _toastService.ShowAsync($"Error inesperado: {ex.Message}", ToastType.Error, position: ToastPosition.Bottom);
            BtnEnviar.IsEnabled = true;
        }
    }
}
