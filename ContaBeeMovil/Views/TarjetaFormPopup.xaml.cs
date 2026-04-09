using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;

namespace ContaBeeMovil.Views;

public partial class TarjetaFormPopup : Popup
{
    private readonly Action<TarjetaModel?> _onResult;
    private readonly TarjetaModel?         _tarjetaEditando;

    public TarjetaFormPopup(Action<TarjetaModel?> onResult, TarjetaModel? tarjeta = null)
    {
        InitializeComponent();
        _onResult        = onResult;
        _tarjetaEditando = tarjeta;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = screenWidth - 40;

        if (tarjeta != null)
        {
            LblTitulo.Text    = "Editar Tarjeta";
            BtnAccion.Text    = "Guardar";
            EntryAlias.Text   = tarjeta.Alias;
            EntryDigitos.Text = tarjeta.UltimosDigitos;
            LblContador.Text  = $"{tarjeta.UltimosDigitos.Length}/4";
        }

        ActualizarBoton();
    }

    private void OnAliasChanged(object sender, TextChangedEventArgs e) => ActualizarBoton();

    private void OnDigitosChanged(object sender, TextChangedEventArgs e)
    {
        // Filtrar solo dígitos y limitar a 4
        var texto = new string((e.NewTextValue ?? string.Empty).Where(char.IsDigit).Take(4).ToArray());
        if (EntryDigitos.Text != texto)
        {
            EntryDigitos.Text = texto;
            return;
        }

        var len = texto.Length;
        LblContador.Text = $"{len}/4";
        VisualStateManager.GoToState(LblContador, len == 0 ? "Empty" : len == 4 ? "Valid" : "Invalid");

        // Al llegar a 4 dígitos cerrar el teclado — impide escribir más
        if (len == 4)
            EntryDigitos.Unfocus();

        ActualizarBoton();
    }

    private void ActualizarBoton()
    {
        var aliasOk   = !string.IsNullOrWhiteSpace(EntryAlias.Text);
        var digitosOk = (EntryDigitos.Text?.Length ?? 0) == 4 && (EntryDigitos.Text?.All(char.IsDigit) ?? false);
        var habilitado = aliasOk && digitosOk;

        BtnAccion.IsEnabled       = habilitado;
        BtnAccion.BackgroundColor = habilitado ? UIHelpers.GetColor("Primary") : UIHelpers.GetColor("Disabled");
        BtnAccion.TextColor       = habilitado ? UIHelpers.GetColor("PrimaryText") : UIHelpers.GetColor("SecondaryText");
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(null);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnAceptar(object sender, EventArgs e)
    {
        var alias   = EntryAlias.Text?.Trim()  ?? string.Empty;
        var digitos = EntryDigitos.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(alias))
        {
            MostrarError("El alias no puede estar vacío.");
            return;
        }

        if (digitos.Length != 4 || !digitos.All(char.IsDigit))
        {
            MostrarError("Ingresa exactamente 4 dígitos.");
            return;
        }

        _onResult(new TarjetaModel
        {
            Id             = _tarjetaEditando?.Id ?? Guid.NewGuid().ToString(),
            Alias          = alias,
            UltimosDigitos = digitos,
        });

        await CloseAsync(CancellationToken.None);
    }

    private void MostrarError(string mensaje)
    {
        LblError.Text      = mensaje;
        LblError.IsVisible = true;
    }
}
