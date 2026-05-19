using CommunityToolkit.Maui.Views;
using Contabee.Api.Transcript;

namespace ContaBeeMovil.Views;

public partial class ActualizarDevolucionPopup : Popup
{
    public record ResultadoActualizar(string? Descripcion);

    private readonly Action<ResultadoActualizar?> _onResult;

    public ActualizarDevolucionPopup(Devolucion devolucion, string rfc, Action<ResultadoActualizar?> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var screenHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = Math.Min(screenWidth - 40, 520);
        CardBorder.MaximumHeightRequest = Math.Min(screenHeight - 80, 420);

        LblEstado.Text = devolucion.Estado.ToString();
        LblCreacion.Text = devolucion.Creacion.ToLocalTime().ToString("dd/MM/yyyy");
        LblRfc.Text = string.IsNullOrWhiteSpace(rfc) ? "RFC no disponible" : rfc;
        EntryDescripcion.Text = devolucion.Descripcion ?? string.Empty;
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(null);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnGuardar(object sender, EventArgs e)
    {
        _onResult(new ResultadoActualizar(EntryDescripcion.Text?.Trim()));
        await CloseAsync(CancellationToken.None);
    }
}
