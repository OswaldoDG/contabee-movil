using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

public partial class CrearDevolucionPopup : Popup
{
    private readonly Action<string?> _onResult;

    public CrearDevolucionPopup(Action<string?> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = Math.Min(screenWidth - 40, 420);
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _onResult(null);
        await CloseAsync(CancellationToken.None);
    }

    private async void OnCrear(object sender, EventArgs e)
    {
        _onResult(EntryDescripcion.Text?.Trim());
        await CloseAsync(CancellationToken.None);
    }
}
