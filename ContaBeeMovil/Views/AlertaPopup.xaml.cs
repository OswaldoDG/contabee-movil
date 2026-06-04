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
        ContenedorPopup.MinimumWidthRequest = screenWidth * 0.70;
        ContenedorPopup.WidthRequest = Math.Min(screenWidth * 0.88, 500);

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
        else if (!verBotonConfirmar)
        {
            Grid.SetColumnSpan(BtnCancelar, 2);
            BtnCancelar.HorizontalOptions = LayoutOptions.Center;
            BtnCancelar.WidthRequest = 130;
        }
        else if (confirmarText.Length > 10 || cancelarText.Length > 10)
        {
            // Texto largo: apila botones verticalmente para que cada uno ocupe el ancho completo
            GridBotones.ColumnDefinitions.Clear();
            GridBotones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            GridBotones.ColumnSpacing = 0;
            GridBotones.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            GridBotones.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            GridBotones.RowSpacing = 8;

            Grid.SetColumn(BtnConfirmar, 0);
            Grid.SetRow(BtnConfirmar, 0);
            Grid.SetColumn(BtnCancelar, 0);
            Grid.SetRow(BtnCancelar, 1);
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
