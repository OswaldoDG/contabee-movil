using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using Contabee.Api.Logging;
using MauiIcons.Core;
using MauiIcons.Material;

namespace ContaBeeMovil.Pages.Perfil;

public partial class EliminarCuentaPage : ContentPage
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IAppLogger _logger;

    private bool _mostrarContrasenas = false;

    public EliminarCuentaPage(
        IServicioIdentidad servicioIdentidad,
        IServicioSesion servicioSesion,
        IServicioAlerta servicioAlerta,
        IAppLogger logger)
    {
        InitializeComponent();
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _servicioAlerta = servicioAlerta;
        _logger = logger;
    }

    private void OnToggleContrasenasClicked(object? sender, TappedEventArgs e)
    {
        _mostrarContrasenas = !_mostrarContrasenas;
        ContrasenaEntry.IsPassword = !_mostrarContrasenas;
        ConfirmarContrasenaEntry.IsPassword = !_mostrarContrasenas;
        ToggleContrasenas.Icon(_mostrarContrasenas ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
    }

    private void OnCampoTextChanged(object? sender, TextChangedEventArgs e)
    {
        var password = ContrasenaEntry.Text ?? string.Empty;
        var confirmar = ConfirmarContrasenaEntry.Text ?? string.Empty;

        var noCoinciden = !string.IsNullOrEmpty(confirmar) && password != confirmar;
        ErrorLabel.Text = "Las contraseñas no coinciden.";
        ErrorLabel.IsVisible = noCoinciden;

        var habilitado = !string.IsNullOrEmpty(password)
                      && !string.IsNullOrEmpty(confirmar)
                      && password == confirmar;

        BtnEliminar.IsEnabled = habilitado;
        BtnEliminar.BackgroundColor = habilitado
            ? UIHelpers.GetColor("Error")
            : UIHelpers.GetColor("Disabled");
    }

    private async void OnEliminarClicked(object? sender, EventArgs e)
    {
        BtnEliminar.IsEnabled = false;

        var confirmado = await _servicioAlerta.MostrarAsync(
            titulo: "¿Eliminar cuenta?",
            mensaje: "Esta acción es permanente. Todos tus datos serán eliminados y no podrás recuperarlos.",
            cancelarText: "Cancelar",
            confirmarText: "Sí, eliminar");

        if (!confirmado)
        {
            BtnEliminar.IsEnabled = true;
            return;
        }

        LoadingOverlay.IsVisible = true;
        try
        {
            _logger.Info("EliminarCuenta.Eliminar", "Inicio de solicitud para eliminar cuenta.");
            var respuesta = await _servicioIdentidad.EliminarCuenta(ContrasenaEntry.Text!);

            if (respuesta.Ok)
            {
                _logger.Info("EliminarCuenta.EliminarExitoso", "Cuenta eliminada correctamente.");
                await _servicioSesion.PostEliminarCuentaAsync();
            }
            else
            {
                var mensaje = respuesta.Error?.Mensaje ?? "No se pudo eliminar la cuenta. Intenta de nuevo.";
                _logger.Debug("EliminarCuenta.EliminarError", "La API devolvió error al eliminar cuenta.", new Dictionary<string, object?>
                {
                    ["Codigo"] = respuesta.Error?.Codigo,
                    ["Mensaje"] = respuesta.Error?.Mensaje,
                    ["HttpCode"] = (int?)respuesta.Error?.HttpCode
                });
                await _servicioAlerta.MostrarAsync(
                    titulo: "Error",
                    mensaje: mensaje,
                    verBotonCancelar: false,
                    confirmarText: "OK");

                BtnEliminar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("EliminarCuenta.EliminarException", "Excepción no controlada al eliminar cuenta.", ex);
            await _servicioAlerta.MostrarAsync(
                titulo: "Error",
                mensaje: "No se pudo eliminar la cuenta. Intenta de nuevo.",
                verBotonCancelar: false,
                confirmarText: "OK");
            BtnEliminar.IsEnabled = true;
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }
}
