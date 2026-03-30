using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

public partial class DireccionFiscalSelectorPopup : Popup
{
    public DireccionFiscalSelectorPopup()
    {
        InitializeComponent();
        ConstruirLista();
    }

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
            PanelLista.Children.Add(UIHelpers.CrearItemSeleccionable(
                GetTextoDisplay(dir), esActual, () => OnSeleccionarDireccion(dir)));
        }
    }

    private void OnSeleccionarDireccion(DireccionFiscal dir)
    {
        AppState.Instance.DireccionFiscalActual = dir;
        _ = CloseAsync();
    }
}
