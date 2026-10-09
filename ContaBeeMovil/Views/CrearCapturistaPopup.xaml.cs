using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using ContaBeeMovil.Helpers;
using MauiIcons.Core;
using MauiIcons.Material;

namespace ContaBeeMovil.Views;

public partial class CrearCapturistaPopup : Popup
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly Guid _cuentaFiscalId;
    private readonly Action<bool> _onResult;
    private bool _mostrarContrasenas = false;

    public CrearCapturistaPopup(IServicioIdentidad servicioIdentidad, Guid cuentaFiscalId, Action<bool> onResult)
    {
        InitializeComponent();
        _servicioIdentidad = servicioIdentidad;
        _cuentaFiscalId    = cuentaFiscalId;
        _onResult          = onResult;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = screenWidth - 40;
    }

    private void OnNombreChanged(object sender, TextChangedEventArgs e) => ActualizarBoton();

    private void OnPasswordChanged(object sender, TextChangedEventArgs e)
    {
        ActualizarReglas();
        ActualizarErrorCoincidencia();
        ActualizarBoton();
    }

    private void OnToggleContrasenasClicked(object? sender, TappedEventArgs e)
    {
        _mostrarContrasenas = !_mostrarContrasenas;
        EntryPassword.IsPassword = !_mostrarContrasenas;
        EntryConfirm.IsPassword = !_mostrarContrasenas;
        ToggleContrasenas.Icon(_mostrarContrasenas ? MaterialIcons.VisibilityOff : MaterialIcons.Visibility);
    }

    private void ActualizarReglas()
    {
        var pwd     = EntryPassword.Text ?? "";
        var success = UIHelpers.GetColor("Primary");
        var disabled = UIHelpers.GetColor("Disabled");

        IconMin6.IconColor     = pwd.Length >= 6 ? success : disabled;
        IconMayus.IconColor    = pwd.Any(char.IsUpper) ? success : disabled;
        IconNumero.IconColor   = pwd.Any(char.IsDigit) ? success : disabled;
        IconEspecial.IconColor = Regex.IsMatch(pwd, @"[@$%&._#]") ? success : disabled;
    }

    private void ActualizarErrorCoincidencia()
    {
        var pwd  = EntryPassword.Text ?? "";
        var conf = EntryConfirm.Text  ?? "";
        LblError.Text      = "Las contraseñas no coinciden";
        LblError.IsVisible = conf.Length > 0 && pwd != conf;
    }

    private void ActualizarBoton()
    {
        var pwd  = EntryPassword.Text ?? "";
        var conf = EntryConfirm.Text  ?? "";

        var nombreOk  = !string.IsNullOrWhiteSpace(EntryNombre.Text);
        var reglasOk  = pwd.Length >= 6
                     && pwd.Any(char.IsUpper)
                     && pwd.Any(char.IsDigit)
                     && Regex.IsMatch(pwd, @"[@$%&._#]");
        var matchOk   = pwd == conf && conf.Length > 0;
        var habilitado = nombreOk && reglasOk && matchOk;

        BtnCrear.IsEnabled       = habilitado;
        BtnCrear.BackgroundColor = habilitado ? UIHelpers.GetColor("Primary")     : UIHelpers.GetColor("Disabled");
        BtnCrear.TextColor       = habilitado ? UIHelpers.GetColor("PrimaryText") : UIHelpers.GetColor("SecondaryText");
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(false);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnCrear(object sender, EventArgs e)
    {
        var nombre   = EntryNombre.Text?.Trim()    ?? "";
        var password = EntryPassword.Text           ?? "";
        var confirm  = EntryConfirm.Text            ?? "";
        var telefono = EntryTelefono.Text?.Trim();

        if (password != confirm)
        {
            LblError.Text      = "Las contraseñas no coinciden";
            LblError.IsVisible = true;
            return;
        }

        BtnCrear.IsEnabled = false;

        var payload = new CreaUsuarioCaptura
        {
            Nombre        = nombre,
            Password      = password,
            Telefono      = string.IsNullOrWhiteSpace(telefono) ? null : telefono,
            CuentaFiscalId = _cuentaFiscalId
        };

        var resp = await _servicioIdentidad.CrearUsuarioCaptura(payload, _cuentaFiscalId);

        if (resp.Ok)
        {
            _onResult(true);
            await CloseAsync(CancellationToken.None);
        }
        else
        {
            LblError.Text      = resp.Error?.Mensaje ?? "Error al crear el usuario.";
            LblError.IsVisible = true;
            BtnCrear.IsEnabled = true;
            ActualizarBoton();
        }
    }
}
