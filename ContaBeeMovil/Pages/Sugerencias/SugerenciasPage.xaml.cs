using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Sugerencias;

public partial class SugerenciasPage : ContentPage
{
    private readonly IServicioToast _servicioToast;
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioLogs _logs;

    public SugerenciasPage(IServicioToast servicioToast, IServicioCrm servicioCrm, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioToast = servicioToast;
        _servicioCrm = servicioCrm;
        _logs = logs;
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
            var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId ?? System.Guid.Empty;

            var request = new DtoCreaRetroalimentacion
            {
                CuentaFiscalId = cuentaFiscalId,
                Elementos = new List<ElementoCreaRetroalimentacion>
                {
                    new() { Tipo = TipoSugerencia.MeGusta,    Detalle = MeGustaEditor.Text?.Trim() ?? string.Empty },
                    new() { Tipo = TipoSugerencia.NoMeGusta,  Detalle = NoMeGustaEditor.Text?.Trim() ?? string.Empty },
                    new() { Tipo = TipoSugerencia.MeGustaria, Detalle = MeGustariaEditor.Text?.Trim() ?? string.Empty }
                }
            };

            var resultado = await _servicioCrm.EnviarFeedback(request);

            MostrarLoader(false);

            if (resultado.Ok)
            {
                await _servicioToast.MostrarAsync("Sugerencia enviada correctamente.", ToastIcono.Info, ToastPosicion.Bottom);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var mensaje = resultado.Error?.Mensaje ?? "Error al enviar la sugerencia.";
                await _servicioToast.MostrarAsync(mensaje, ToastIcono.Error, ToastPosicion.Bottom);
                BtnEnviar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            MostrarLoader(false);
            _logs.Log($"[SugerenciasPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioToast.MostrarAsync("Ocurrió un error al enviar la sugerencia.", ToastIcono.Error, ToastPosicion.Bottom);
            BtnEnviar.IsEnabled = true;
        }
    }
}
