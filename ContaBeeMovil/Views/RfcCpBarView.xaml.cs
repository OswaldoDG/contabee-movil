using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

public partial class RfcCpBarView : ContentView
{
    public RfcCpBarView()
    {
        InitializeComponent();
        ActualizarDatos();

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.CuentaFiscalActual)
                               or nameof(AppState.MostrarNombreFiscal)
                               or nameof(AppState.DireccionFiscalActual))
                MainThread.BeginInvokeOnMainThread(ActualizarDatos);
        };
    }

    private void ActualizarDatos()
    {
        var cuenta = AppState.Instance.CuentaFiscalActual;

        LabelRfc.Text = GetTextoIzquierda(cuenta);

        var dir = AppState.Instance.DireccionFiscalActual
                  ?? cuenta?.DireccionesFiscales?.FirstOrDefault();
        LabelCp.Text = string.IsNullOrWhiteSpace(dir?.CodigoPostal) ? "?" : dir.CodigoPostal;
    }

    private static string GetTextoIzquierda(AsociacionCuentaFiscalCompleta? cuenta)
    {
        if (cuenta == null) return "?";

        if (AppState.Instance.MostrarNombreFiscal)
        {
            var nombre = cuenta.DireccionesFiscales?.FirstOrDefault()?.CuentaFiscal?.Nombre;
            return string.IsNullOrWhiteSpace(nombre) ? "?" : nombre;
        }
        return string.IsNullOrWhiteSpace(cuenta.Rfc) ? "?" : cuenta.Rfc;
    }

    private async void OnRfcButtonTapped(object? sender, TappedEventArgs e)
    {
        var page = Shell.Current as Page ?? Application.Current!.Windows[0].Page!;
        await page.ShowPopupAsync(new CuentaFiscalSelectorPopup());
    }

    private async void OnCpButtonTapped(object? sender, TappedEventArgs e)
    {
        var page = Shell.Current as Page ?? Application.Current!.Windows[0].Page!;
        await page.ShowPopupAsync(new DireccionFiscalSelectorPopup());
    }
}
