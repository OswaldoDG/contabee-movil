using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Confirmar;

[QueryProperty(nameof(Token), "token")]
public partial class ConfirmarCuentaPage : ContentPage
{
    private string _token = string.Empty;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioLogs _logs;
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

    public ConfirmarCuentaPage(IServicioIdentidad servicioIdentidad, IServicioLogs logs)
    {
        InitializeComponent();
        this._servicioIdentidad = servicioIdentidad;
        this._logs = logs;
    }

    private async void ActivarCuentaAsync(string token)
    {
        MostrarEstado(Estado.Cargando);

        try
        {
            var respuesta = await _servicioIdentidad.ConfirmarCuenta(token);
            if (respuesta.Ok)
            {
                _activacionExitosa = true;
                MostrarEstado(Estado.Exito);
            }
            else
            {
                _logs.Log($"[ConfirmarCuentaPage] Error API: {respuesta.Error?.Codigo} - {respuesta.Error?.Mensaje}");
                MostrarEstado(Estado.Error, "El enlace no es válido o ya fue usado.");
            }
        }
        catch (Exception ex)
        {
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
