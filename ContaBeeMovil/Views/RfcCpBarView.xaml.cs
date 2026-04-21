using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

public partial class RfcCpBarView : ContentView
{
    public static readonly BindableProperty ConFondoProperty =
        BindableProperty.Create(
            nameof(ConFondo),
            typeof(bool),
            typeof(RfcCpBarView),
            defaultValue: true);

    public bool ConFondo
    {
        get => (bool)GetValue(ConFondoProperty);
        set => SetValue(ConFondoProperty, value);
    }

    public static readonly BindableProperty EnColumnasProperty =
        BindableProperty.Create(
            nameof(EnColumnas),
            typeof(bool),
            typeof(RfcCpBarView),
            defaultValue: false);

    public bool EnColumnas
    {
        get => (bool)GetValue(EnColumnasProperty);
        set => SetValue(EnColumnasProperty, value);
    }

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
        var rfcText = GetTextoRfc(cuenta);

        var dir = AppState.Instance.DireccionFiscalActual
                  ?? cuenta?.DireccionesFiscales?.FirstOrDefault();
        var cpText = string.IsNullOrWhiteSpace(dir?.CodigoPostal) ? "?" : dir.CodigoPostal;

        LabelRfcCp.Text = $"{rfcText} - {cpText}";
    }

    private static string GetTextoRfc(AsociacionCuentaFiscalCompleta? cuenta)
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
