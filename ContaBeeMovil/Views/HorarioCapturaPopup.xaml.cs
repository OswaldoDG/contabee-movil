using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

public partial class HorarioCapturaPopup : Popup
{
    /// <param name="mensaje">Leyenda ya redactada por <c>IServicioHorarioCaptura</c>.</param>
    public HorarioCapturaPopup(string mensaje)
    {
        InitializeComponent();

        var anchoPantalla = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        ContenedorPopup.MinimumWidthRequest = anchoPantalla * 0.70;
        ContenedorPopup.WidthRequest = Math.Min(anchoPantalla * 0.86, 420);

        LblMensaje.Text = mensaje;
    }

    private void OnCerrar(object? sender, EventArgs e) => _ = CloseAsync();
}
