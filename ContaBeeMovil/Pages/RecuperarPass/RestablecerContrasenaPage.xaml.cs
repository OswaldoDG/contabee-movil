using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Text.RegularExpressions;

namespace ContaBeeMovil.Pages.RecuperarPass;

public partial class RestablecerContrasenaPage : ContentPage
{
    private readonly IServicioToast _servicioToast;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioLogs _logs;
    private readonly IAppLogger _logger;

    public string Token { get; set; } = string.Empty;

    public RestablecerContrasenaPage(IServicioToast servicioToast, IServicioIdentidad servicioIdentidad, IServicioLogs logs, IAppLogger logger)
    {
        InitializeComponent();
        _servicioToast = servicioToast;
        _servicioIdentidad = servicioIdentidad;
        _logs = logs;
        _logger = logger;
    }

    private void OnToggleNuevaContrasenaClicked(object? sender, TappedEventArgs e)
    {
        NuevaContrasenaEntry.IsPassword = !NuevaContrasenaEntry.IsPassword;
        ToggleNuevaContrasena.Icon(NuevaContrasenaEntry.IsPassword
            ? MaterialIcons.Visibility
            : MaterialIcons.VisibilityOff);
    }

    private void OnToggleConfirmarContrasenaClicked(object? sender, TappedEventArgs e)
    {
        ConfirmarContrasenaEntry.IsPassword = !ConfirmarContrasenaEntry.IsPassword;
        ToggleConfirmarContrasena.Icon(ConfirmarContrasenaEntry.IsPassword
            ? MaterialIcons.Visibility
            : MaterialIcons.VisibilityOff);
    }

    private void OnNuevaContrasenaTextChanged(object? sender, TextChangedEventArgs e)
    {
        ActualizarIconosValidacion(e.NewTextValue ?? string.Empty);
        ActualizarEstadoBoton();
    }

    private void OnCampoTextChanged(object? sender, TextChangedEventArgs e)
    {
        ActualizarEstadoBoton();
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
        Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
    }

    private async void OnRestablecerClicked(object? sender, EventArgs e)
    {
        var nueva = NuevaContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        if (nueva != confirmar)
        {
            await _servicioToast.MostrarAsync("Las contraseñas no coinciden.", ToastIcono.Error, ToastPosicion.Bottom);
            return;
        }

        BtnRestablecer.IsEnabled = false;
        MostrarLoader(true);

        try
        {
            _logger.Info("RestablecerContrasena.Restablecer", "Inicio de solicitud para restablecer contraseña.");
            var resultado = await _servicioIdentidad.RestablecerContrasena(nueva, Token);

            if (resultado.Ok)
            {
                _logger.Info("RestablecerContrasena.RestablecerExitoso", "Restablecimiento de contraseña completado correctamente.");
                await _servicioToast.MostrarAsync("Contraseña restablecida correctamente.", ToastIcono.Info, ToastPosicion.Bottom);

                var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
                Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
            }
            else
            {
                _logger.Debug("RestablecerContrasena.RestablecerError", "La API devolvió error al restablecer contraseña.", new Dictionary<string, object?>
                {
                    ["Codigo"] = resultado.Error?.Codigo,
                    ["Mensaje"] = resultado.Error?.Mensaje,
                    ["HttpCode"] = (int?)resultado.Error?.HttpCode
                });
                _logs.Log($"[RestablecerContrasenaPage] Error API: {resultado.Error?.Codigo} - {resultado.Error?.Mensaje}");
                await _servicioToast.MostrarAsync("Error al restablecer la contraseña.", ToastIcono.Error, ToastPosicion.Bottom);
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("RestablecerContrasena.RestablecerException", "Excepción no controlada al restablecer contraseña.", ex);
            _logs.Log($"[RestablecerContrasenaPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioToast.MostrarAsync("Error al restablecer la contraseña.", ToastIcono.Error, ToastPosicion.Bottom);
        }
        finally
        {
            MostrarLoader(false);
            ActualizarEstadoBoton();
        }
    }

    private void ActualizarIconosValidacion(string pwd)
    {
        var success = UIHelpers.GetColor("Primary");
        var disabled = UIHelpers.GetColor("Disabled");

        var esMin6 = pwd.Length >= 6;
        var tieneMayus = pwd.Any(char.IsUpper);
        var tieneNumero = pwd.Any(char.IsDigit);
        var tieneEspecial = Regex.IsMatch(pwd, @"[@#\$%&._]");

        IconMin6.IconColor = esMin6 ? success : disabled;
        IconMayus.IconColor = tieneMayus ? success : disabled;
        IconNumero.IconColor = tieneNumero ? success : disabled;
        IconEspecial.IconColor = tieneEspecial ? success : disabled;
    }

    private void ActualizarEstadoBoton()
    {
        var nueva = NuevaContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        var camposLlenos = !string.IsNullOrEmpty(nueva)
                        && !string.IsNullOrEmpty(confirmar);

        var coinciden = nueva == confirmar;

        var validaciones = nueva.Length >= 6
                        && nueva.Any(char.IsUpper)
                        && nueva.Any(char.IsDigit)
                        && Regex.IsMatch(nueva, @"[@#\$%&._]");

        ErrorCoincidenciaLabel.IsVisible = !string.IsNullOrEmpty(confirmar) && !coinciden;

        BtnRestablecer.IsEnabled = camposLlenos && coinciden && validaciones;
    }

    private void MostrarLoader(bool visible)
    {
        Loader.IsRunning = visible;
        Loader.Opacity = visible ? 1 : 0;
    }
}
