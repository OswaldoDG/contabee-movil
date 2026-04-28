using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
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

    public string Token { get; set; } = string.Empty;

    public RestablecerContrasenaPage(IServicioToast servicioToast, IServicioIdentidad servicioIdentidad, IServicioLogs logs)
    {
        InitializeComponent();
        _servicioToast = servicioToast;
        _servicioIdentidad = servicioIdentidad;
        _logs = logs;
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
        Application.Current!.Windows[0].Page = paginaLogin;
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
            var resultado = await _servicioIdentidad.RestablecerContrasena(nueva, Token);

            if (resultado.Ok)
            {
                await _servicioToast.MostrarAsync("Contraseña restablecida correctamente.", ToastIcono.Info, ToastPosicion.Bottom);

                var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
                Application.Current!.Windows[0].Page = paginaLogin;
            }
            else
            {
                _logs.Log($"[RestablecerContrasenaPage] Error API: {resultado.Error?.Codigo} - {resultado.Error?.Mensaje}");
                await _servicioToast.MostrarAsync("Error al restablecer la contraseña.", ToastIcono.Error, ToastPosicion.Bottom);
            }
        }
        catch (Exception ex)
        {
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
