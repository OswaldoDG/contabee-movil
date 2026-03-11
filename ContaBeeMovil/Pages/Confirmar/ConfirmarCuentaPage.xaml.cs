using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Confirmar;

[QueryProperty(nameof(Token), "token")]
public partial class ConfirmarCuentaPage : ContentPage
{
    private string _token = string.Empty;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IToastService _toastService;

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

    public ConfirmarCuentaPage(IServicioIdentidad servicioIdentidad, IToastService ToastService)
    {
        InitializeComponent();
        this._servicioIdentidad = servicioIdentidad;
        this._toastService = ToastService;
    }

    private async void ActivarCuentaAsync(string token)
    {
        MostrarEstado(Estado.Cargando);

        try
        {
            var respuesta = await _servicioIdentidad.ConfirmarCuenta(token);
            if (respuesta.Ok)
            {
                MostrarEstado(Estado.Exito);
            }else
            {
                var mensaje = respuesta.Error?.Mensaje.Split("Response")[1];

                System.Diagnostics.Debug.WriteLine($"❌ API Error: {respuesta.Error?.Mensaje}");
                MostrarEstado(Estado.Error, mensaje?? "Error desconocido.");
            }

            
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
            MostrarEstado(Estado.Error, "El enlace no es válido o ya fue usado.");
        }
    }

    private void MostrarEstado(Estado estado, string? mensajeError = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Ocultar todo primero
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
        var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
        Application.Current!.Windows[0].Page = paginaLogin;
    }

    private enum Estado { Cargando, Exito, Error }
}