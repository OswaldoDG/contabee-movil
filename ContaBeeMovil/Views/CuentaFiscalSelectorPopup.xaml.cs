using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Services.Device;
using Microsoft.Maui.Controls.Shapes;

namespace ContaBeeMovil.Views;

public partial class CuentaFiscalSelectorPopup : Popup
{
    private static readonly Color PrimaryColor  = Color.FromArgb("#f4c611");
    private static readonly Color ItemBgColor   = Color.FromArgb("#f0f0f0");
    private static readonly Color TextDark      = Color.FromArgb("#1e1e1e");
    private static readonly Color TextGray      = Color.FromArgb("#9e9e9e");

    public CuentaFiscalSelectorPopup()
    {
        InitializeComponent();
        ActualizarToggle();
        ConstruirLista();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetTextoDisplay(AsociacionCuentaFiscalCompleta cuenta)
    {
        if (AppState.Instance.MostrarNombreFiscal)
        {
            // TODO: usar cuenta.Nombre cuando la API lo incluya en AsociacionCuentaFiscalCompleta
            var nombre = cuenta.DireccionesFiscales.FirstOrDefault().CuentaFiscal.Nombre; // fallback
            return string.IsNullOrWhiteSpace(nombre) ? "?" : nombre;
        }
        return string.IsNullOrWhiteSpace(cuenta.Rfc) ? "?" : cuenta.Rfc;
    }

    private void ConstruirLista()
    {
        PanelLista.Children.Clear();

        var cuentas = AppState.Instance.CuentasFiscales;
        if (cuentas == null) return;

        var actual = AppState.Instance.CuentaFiscalActual;

        foreach (var cuenta in cuentas)
        {
            bool esActual = actual != null && cuenta.CuentaFiscalId == actual.CuentaFiscalId;
            PanelLista.Children.Add(CrearItem(cuenta, esActual));
        }
    }

    private Border CrearItem(AsociacionCuentaFiscalCompleta cuenta, bool seleccionado)
    {
        var label = new Label
        {
            Text           = GetTextoDisplay(cuenta),
            FontAttributes = FontAttributes.Bold,
            FontSize       = 15,
            HorizontalOptions = LayoutOptions.Center,
            TextColor      = seleccionado ? TextDark : TextGray,
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
        tap.Tapped += (_, _) => OnSeleccionarCuenta(cuenta);
        borde.GestureRecognizers.Add(tap);

        return borde;
    }

    // ── Eventos ───────────────────────────────────────────────────────────────

    private void OnSeleccionarCuenta(AsociacionCuentaFiscalCompleta cuenta)
    {
        AppState.Instance.CuentaFiscalActual = cuenta;
        _ = CloseAsync();
    }

    private void OnToggleRfc(object? sender, TappedEventArgs e)
    {
        AppState.Instance.MostrarNombreFiscal = false;
        ActualizarToggle();
        ConstruirLista();
    }

    private void OnToggleNombre(object? sender, TappedEventArgs e)
    {
        AppState.Instance.MostrarNombreFiscal = true;
        ActualizarToggle();
        ConstruirLista();
    }

    private void ActualizarToggle()
    {
        bool nombre = AppState.Instance.MostrarNombreFiscal;

        BgRfc.BackgroundColor    = nombre ? Colors.Transparent : PrimaryColor;
        LblRfc.TextColor         = nombre ? TextGray : TextDark;
        LblRfc.FontAttributes    = nombre ? FontAttributes.None : FontAttributes.Bold;

        BgNombre.BackgroundColor = nombre ? PrimaryColor : Colors.Transparent;
        LblNombre.TextColor      = nombre ? TextDark : TextGray;
        LblNombre.FontAttributes = nombre ? FontAttributes.Bold : FontAttributes.None;
    }
}
