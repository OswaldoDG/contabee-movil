using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

public partial class AlertaPopup : Popup
{
    public bool Confirmado { get; private set; }

    public AlertaPopup(
        string titulo,
        string mensaje,
        bool verBotonCancelar = true,
        bool verBotonConfirmar = true,
        string cancelarText = "Cancelar",
        string confirmarText = "Si")
    {
        InitializeComponent();

        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        ContenedorPopup.WidthRequest = Math.Min(screenWidth - 48, 320);

        LblTitulo.Text = titulo;
        LblMensaje.Text = mensaje;

        BtnCancelar.Text = cancelarText;
        BtnCancelar.IsVisible = verBotonCancelar;

        BtnConfirmar.Text = confirmarText;
        BtnConfirmar.IsVisible = verBotonConfirmar;

        if (!verBotonCancelar)
        {
            Grid.SetColumn(BtnConfirmar, 0);
            Grid.SetColumnSpan(BtnConfirmar, 2);
            BtnConfirmar.HorizontalOptions = LayoutOptions.Center;
            BtnConfirmar.WidthRequest = 130;
        }
        if (!verBotonConfirmar)
        {
            Grid.SetColumnSpan(BtnCancelar, 2);
            BtnCancelar.HorizontalOptions = LayoutOptions.Center;
            BtnCancelar.WidthRequest = 130;
        }
    }

    private void OnCancelar(object sender, EventArgs e)
    {
        Confirmado = false;
        _ = CloseAsync();
    }

    private void OnConfirmar(object sender, EventArgs e)
    {
        Confirmado = true;
        _ = CloseAsync();
    }
}
