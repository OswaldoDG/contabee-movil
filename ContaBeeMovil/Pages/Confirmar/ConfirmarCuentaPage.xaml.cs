using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Confirmar;

[QueryProperty(nameof(Token), "token")]
public partial class ConfirmarCuentaPage : ContentPage
{
    private string _token = string.Empty;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioLogs _logs;
    private readonly IAppLogger _logger;
    private bool _activacionExitosa;

    public string Token
    {
        get => _token;
        set
        {
            _token = Uri.UnescapeDataString(value ?? string.Empty);
            OnPropertyChanged();
            ActivarCuentaAsync(_token);
        }
    }

    public ConfirmarCuentaPage(IServicioIdentidad servicioIdentidad, IServicioLogs logs, IAppLogger logger)
    {
        InitializeComponent();
        this._servicioIdentidad = servicioIdentidad;
        this._logs = logs;
        this._logger = logger;
    }

    private async void ActivarCuentaAsync(string token)
    {
        MostrarEstado(Estado.Cargando);

        try
        {
            _logger.Info("ConfirmarCuenta.Activar", "Inicio de confirmación de cuenta.");
            var respuesta = await _servicioIdentidad.ConfirmarCuenta(token);
            if (respuesta.Ok)
            {
                _activacionExitosa = true;
                _logger.Info("ConfirmarCuenta.ActivarExitoso", "Cuenta confirmada correctamente.");
                MostrarEstado(Estado.Exito);
            }
            else
            {
                _logger.Debug("ConfirmarCuenta.ActivarError", "La API devolvió error en confirmación de cuenta.", new Dictionary<string, object?>
                {
                    ["Codigo"] = respuesta.Error?.Codigo,
                    ["Mensaje"] = respuesta.Error?.Mensaje,
                    ["HttpCode"] = (int?)respuesta.Error?.HttpCode
                });
                _logs.Log($"[ConfirmarCuentaPage] Error API: {respuesta.Error?.Codigo} - {respuesta.Error?.Mensaje}");
                MostrarEstado(Estado.Error, "El enlace no es válido o ya fue usado.");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("ConfirmarCuenta.ActivarException", "Excepción no controlada al confirmar cuenta.", ex);
            _logs.Log($"[ConfirmarCuentaPage] {ex.GetType().Name}: {ex.Message}");
            MostrarEstado(Estado.Error, "El enlace no es válido o ya fue usado.");
        }
    }

    private void MostrarEstado(Estado estado, string? mensajeError = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            SuccessIcon.IsVisible = false;
            ErrorIcon.IsVisible = false;
            ContinuarBtn.IsVisible = false;

            switch (estado)
            {
                case Estado.Cargando:
                    LoadingIndicator.IsVisible = true;
                    LoadingIndicator.IsRunning = true;
                    TituloLabel.Text = "Verificando tu cuenta...";
                    MensajeLabel.Text = "Por favor espera un momento.";
                    break;

                case Estado.Exito:
                    SuccessIcon.IsVisible = true;
                    TituloLabel.Text = "¡Cuenta activada!";
                    MensajeLabel.Text = "Tu cuenta ha sido verificada exitosamente.";
                    ContinuarBtn.IsVisible = true;
                    break;

                case Estado.Error:
                    ErrorIcon.IsVisible = true;
                    TituloLabel.Text = "Error al activar";
                    MensajeLabel.Text = mensajeError ?? "Algo salió mal.";
                    ContinuarBtn.IsVisible = true;
                    break;
            }
        });
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        if (_activacionExitosa)
        {
            PaginaLogin.LimpiarAlNavegar = true;
        }
        var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
        Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
    }

    private enum Estado { Cargando, Exito, Error }
}
