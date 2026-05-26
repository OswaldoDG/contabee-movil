using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Text.RegularExpressions;

namespace ContaBeeMovil.Pages.Perfil;

public partial class CambiarContrasenaPage : ContentPage
{
    private readonly IServicioToast _servicioToast;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IAppLogger _logger;

    private bool _mostrarContrasenas = false;

    public CambiarContrasenaPage(IServicioToast servicioToast, IServicioIdentidad servicioIdentidad, IAppLogger logger)
    {
        InitializeComponent();
        _servicioToast = servicioToast;
        _servicioIdentidad = servicioIdentidad;
        _logger = logger;
    }

    private void OnToggleContrasenasClicked(object? sender, TappedEventArgs e)
    {
        _mostrarContrasenas = !_mostrarContrasenas;
        ContrasenaActualEntry.IsPassword = !_mostrarContrasenas;
        NuevaContrasenaEntry.IsPassword = !_mostrarContrasenas;
        ConfirmarContrasenaEntry.IsPassword = !_mostrarContrasenas;
        ToggleContrasenas.Icon(_mostrarContrasenas ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
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

    private async void OnActualizarClicked(object? sender, EventArgs e)
    {
        var actual = ContrasenaActualEntry.Text ?? string.Empty;
        var nueva = NuevaContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        if (nueva != confirmar)
        {
            await _servicioToast.MostrarAsync("Las contraseñas no coinciden.", ToastIcono.Error);
            return;
        }

        BtnActualizar.IsEnabled = false;
        _logger.Info("CambiarContrasena.Actualizar", "Inicio de solicitud para cambiar contraseña.");
        try
        {
            var resultado = await _servicioIdentidad.CambiarContrasena(actual, nueva);

            if (resultado.Ok)
            {
                _logger.Info("CambiarContrasena.ActualizarExitoso", "Cambio de contraseña completado correctamente.");
                await _servicioToast.MostrarAsync("Contraseña actualizada correctamente.", ToastIcono.Info);
                await Shell.Current.GoToAsync("..");
                return;
            }

            var mensaje = resultado.Error?.Mensaje ?? "Error al cambiar la contraseña.";
            _logger.Debug("CambiarContrasena.ActualizarError", "La API devolvió un error al cambiar contraseña.", new Dictionary<string, object?>
            {
                ["Codigo"] = resultado.Error?.Codigo,
                ["Mensaje"] = resultado.Error?.Mensaje,
                ["HttpCode"] = (int?)resultado.Error?.HttpCode
            });
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[CambiarContrasena] Error: Codigo={resultado.Error?.Codigo}, Mensaje={resultado.Error?.Mensaje}, HttpCode={resultado.Error?.HttpCode}");
#endif
            await _servicioToast.MostrarAsync(mensaje, ToastIcono.Error);
        }
        catch (Exception ex)
        {
            _logger.Debug("CambiarContrasena.ActualizarException", "Excepción no controlada al cambiar contraseña.", ex);
            await _servicioToast.MostrarAsync("Error al cambiar la contraseña.", ToastIcono.Error);
        }
        finally
        {
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
        var actual = ContrasenaActualEntry.Text ?? string.Empty;
        var nueva = NuevaContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        var camposLlenos = !string.IsNullOrEmpty(actual)
                        && !string.IsNullOrEmpty(nueva)
                        && !string.IsNullOrEmpty(confirmar);

        var coinciden = nueva == confirmar;

        var validaciones = nueva.Length >= 6
                        && nueva.Any(char.IsUpper)
                        && nueva.Any(char.IsDigit)
                        && Regex.IsMatch(nueva, @"[@#\$%&._]");

        ErrorCoincidenciaLabel.IsVisible = !string.IsNullOrEmpty(confirmar) && !coinciden;

        BtnActualizar.IsEnabled = camposLlenos && coinciden && validaciones;
    }
}
