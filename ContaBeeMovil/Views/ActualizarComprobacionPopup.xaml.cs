using CommunityToolkit.Maui.Views;
using Contabee.Api.Transcript;

namespace ContaBeeMovil.Views;

public partial class ActualizarComprobacionPopup : Popup
{
    public record ResultadoActualizar(decimal Monto, int PorcentajeComprobar, DateTime Cierre, string? Descripcion);

    private readonly Action<ResultadoActualizar?> _onResult;

    public ActualizarComprobacionPopup(Comprobacion comprobacion, string rfc, Action<ResultadoActualizar?> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var screenHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = Math.Min(screenWidth - 40, 560);
        CardBorder.MaximumHeightRequest = Math.Min(screenHeight - 80, 540);

        LblCreacion.Text = comprobacion.Creacion.ToLocalTime().ToString("dd/MM/yyyy");
        PickerCierre.Date = (comprobacion.Cierre ?? DateTime.Today).Date;
        LblRfc.Text = string.IsNullOrWhiteSpace(rfc) ? "RFC no disponible" : rfc;
        EntryMonto.Text = comprobacion.Monto.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        LblComprobado.Text = comprobacion.MontoComprobado.ToString("0.00");
        EntryPorcentaje.Text = comprobacion.PorcentajeCompropbar.ToString();
        EntryDescripcion.Text = comprobacion.Descripcion ?? string.Empty;
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(null);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnGuardar(object sender, EventArgs e)
    {
        if (!decimal.TryParse(EntryMonto.Text, out var monto) || monto < 0)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page1)
                await page1.DisplayAlert("Validación", "Ingresa un monto válido.", "OK");
            return;
        }

        if (!int.TryParse(EntryPorcentaje.Text, out var porcentaje) || porcentaje < 0 || porcentaje > 100)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page2)
                await page2.DisplayAlert("Validación", "Ingresa un porcentaje entre 0 y 100.", "OK");
            return;
        }

        _onResult(new ResultadoActualizar(monto, porcentaje, PickerCierre.Date ?? DateTime.Today, EntryDescripcion.Text?.Trim()));
        await CloseAsync(CancellationToken.None);
    }
}
