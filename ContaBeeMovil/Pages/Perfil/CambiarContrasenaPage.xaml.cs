using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Text.RegularExpressions;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Pages.Perfil;

public partial class CambiarContrasenaPage : ContentPage
{
    private readonly IToastService _toastService;
    private readonly IServicioIdentidad _servicioIdentidad;

    public CambiarContrasenaPage(IToastService toastService, IServicioIdentidad servicioIdentidad)
    {
        InitializeComponent();
        _toastService = toastService;
        _servicioIdentidad = servicioIdentidad;
    }

    private void OnToggleContrasenaActualClicked(object? sender, TappedEventArgs e)
    {
        ContrasenaActualEntry.IsPassword = !ContrasenaActualEntry.IsPassword;
        ToggleContrasenaActual.Icon(ContrasenaActualEntry.IsPassword
            ? MaterialIcons.Visibility
            : MaterialIcons.VisibilityOff);
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

    private async void OnActualizarClicked(object? sender, EventArgs e)
    {
        var actual = ContrasenaActualEntry.Text ?? string.Empty;
        var nueva = NuevaContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        if (nueva != confirmar)
        {
            await _toastService.ShowAsync("Las contraseñas no coinciden.", ToastType.Error);
            return;
        }

        BtnActualizar.IsEnabled = false;

        var resultado = await _servicioIdentidad.CambiarContrasena(actual, nueva);

        if (resultado.Ok)
        {
            await _toastService.ShowAsync("Contraseña actualizada correctamente.", ToastType.Success);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            var mensaje = resultado.Error?.Mensaje ?? "Error al cambiar la contraseña.";
            System.Diagnostics.Debug.WriteLine($"[CambiarContrasena] Error: Codigo={resultado.Error?.Codigo}, Mensaje={resultado.Error?.Mensaje}, HttpCode={resultado.Error?.HttpCode}");
            await _toastService.ShowAsync(mensaje, ToastType.Error);
        }

        ActualizarEstadoBoton();
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
