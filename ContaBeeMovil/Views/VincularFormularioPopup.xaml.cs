using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Pages.Equipo;

namespace ContaBeeMovil.Views;

public partial class VincularFormularioPopup : Popup
{
    private readonly VincularViewModel _vm;

    public VincularFormularioPopup(VincularViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        CardBorder.WidthRequest = screenWidth - 48;
    }

    private async void OnCancelar(object sender, EventArgs e)
    {
        _vm.CancelarFormularioCommand.Execute(null);
        await CloseAsync(CancellationToken.None);
    }
}
