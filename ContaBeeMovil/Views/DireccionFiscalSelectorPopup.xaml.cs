using CommunityToolkit.Maui.Views;
using Contabee.Api.crm;
using ContaBeeMovil.Services.Device;
using Microsoft.Maui.Controls.Shapes;

namespace ContaBeeMovil.Views;

public partial class DireccionFiscalSelectorPopup : Popup
{
    private static readonly Color PrimaryColor = Color.FromArgb("#f4c611");
    private static readonly Color ItemBgColor  = Color.FromArgb("#f0f0f0");
    private static readonly Color TextDark     = Color.FromArgb("#1e1e1e");
    private static readonly Color TextGray     = Color.FromArgb("#9e9e9e");

    public DireccionFiscalSelectorPopup()
    {
        InitializeComponent();
        ConstruirLista();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetTextoDisplay(DireccionFiscal dir)
    {
        var cp = string.IsNullOrWhiteSpace(dir.CodigoPostal) ? "?" : dir.CodigoPostal;

        if (!string.IsNullOrWhiteSpace(dir.Colonia))
            return $"{cp} – {dir.Colonia}";

        return cp;
    }

    private void ConstruirLista()
    {
        PanelLista.Children.Clear();

        var direcciones = AppState.Instance.CuentaFiscalActual?.DireccionesFiscales;
        if (direcciones == null || direcciones.Count == 0) return;

        var actual = AppState.Instance.DireccionFiscalActual;

        foreach (var dir in direcciones)
        {
            bool esActual = actual != null && dir.Id == actual.Id;
            PanelLista.Children.Add(CrearItem(dir, esActual));
        }
    }

    private Border CrearItem(DireccionFiscal dir, bool seleccionado)
    {
        var label = new Label
        {
            Text              = GetTextoDisplay(dir),
            FontAttributes    = FontAttributes.Bold,
            FontSize          = 15,
            HorizontalOptions = LayoutOptions.Center,
            TextColor         = seleccionado ? TextDark : TextGray,
        };

        var borde = new Border
        {
            BackgroundColor = seleccionado ? PrimaryColor : ItemBgColor,
            StrokeThickness = 0,
            StrokeShape     = new RoundRectangle { CornerRadius = 12 },
            Padding         = new Thickness(16, 14),
            Content         = label,
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnSeleccionarDireccion(dir);
        borde.GestureRecognizers.Add(tap);

        return borde;
    }

    // ── Eventos ───────────────────────────────────────────────────────────────

    private void OnSeleccionarDireccion(DireccionFiscal dir)
    {
        AppState.Instance.DireccionFiscalActual = dir;
        _ = CloseAsync();
    }
}
