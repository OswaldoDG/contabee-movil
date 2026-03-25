using CommunityToolkit.Maui.Views;
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

        if (tarjeta != null)
        {
            LblTitulo.Text      = "Editar Tarjeta";
            BtnAccion.Text      = "Guardar";
            EntryAlias.Text     = tarjeta.Alias;
            EntryDigitos.Text   = tarjeta.UltimosDigitos;
            LblContador.Text    = $"{tarjeta.UltimosDigitos.Length}/4";
        }
    }

    private void OnDigitosChanged(object sender, TextChangedEventArgs e)
    {
        var len = e.NewTextValue?.Length ?? 0;
        LblContador.Text = $"{len}/4";

        var state = len == 0 ? "Empty" : len == 4 ? "Valid" : "Invalid";
        VisualStateManager.GoToState(LblContador, state);
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
