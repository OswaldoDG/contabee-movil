using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Notifications;
using MauiIcons.Core;
using MauiIcons.Material;
using System.Text.RegularExpressions;

namespace ContaBeeMovil.Pages.RecuperarPass;

public partial class RestablecerContrasenaPage : ContentPage
{
    private readonly IToastService _toastService;
    private readonly IServicioIdentidad _servicioIdentidad;

    public string Token { get; set; } = string.Empty;

    public RestablecerContrasenaPage(IToastService toastService, IServicioIdentidad servicioIdentidad)
    {
        InitializeComponent();
        _toastService = toastService;
        _servicioIdentidad = servicioIdentidad;
    }

    // ── Toggle visibilidad ──────────────────────────────────────────

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

    // ── Validación en tiempo real ───────────────────────────────────

    private void OnNuevaContrasenaTextChanged(object? sender, TextChangedEventArgs e)
    {
        ActualizarIconosValidacion(e.NewTextValue ?? string.Empty);
        ActualizarEstadoBoton();
    }

    private void OnCampoTextChanged(object? sender, TextChangedEventArgs e)
    {
        ActualizarEstadoBoton();
    }

    // ── Acción principal ────────────────────────────────────────────

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
            await _toastService.ShowAsync("Las contraseñas no coinciden.", ToastType.Error, position: ToastPosition.Bottom);
            return;
        }

        BtnRestablecer.IsEnabled = false;
        MostrarLoader(true);

        try
        {
            var resultado = await _servicioIdentidad.RestablecerContrasena(nueva, Token);

            if (resultado.Ok)
            {
                await _toastService.ShowAsync("Contraseña restablecida correctamente.", ToastType.Success, position: ToastPosition.Bottom);

                // Redirigir al login
                var paginaLogin = MauiProgram.Services.GetRequiredService<PaginaLogin>();
                Application.Current!.Windows[0].Page = paginaLogin;
            }
            else
            {
                var mensaje = resultado.Error?.Mensaje ?? "Error al restablecer la contraseña.";
                System.Diagnostics.Debug.WriteLine($"[RestablecerContrasena] Error: {resultado.Error?.Codigo} - {mensaje}");
                await _toastService.ShowAsync(mensaje, ToastType.Error, position: ToastPosition.Bottom);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RestablecerContrasena] Exception: {ex.Message}");
            await _toastService.ShowAsync("Error al restablecer la contraseña.", ToastType.Error, position: ToastPosition.Bottom);
        }
        finally
        {
            MostrarLoader(false);
            ActualizarEstadoBoton();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────

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
