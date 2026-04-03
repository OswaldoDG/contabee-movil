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
            defaultValue: true,
            propertyChanged: OnConFondoChanged);

    public bool ConFondo
    {
        get => (bool)GetValue(ConFondoProperty);
        set => SetValue(ConFondoProperty, value);
    }

    private static void OnConFondoChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RfcCpBarView view)
            view.RootGrid.BackgroundColor = (bool)newValue
                ? (Color)Application.Current!.Resources["Primary"]
                : Colors.Transparent;
    }

    public static readonly BindableProperty EnColumnasProperty =
        BindableProperty.Create(
            nameof(EnColumnas),
            typeof(bool),
            typeof(RfcCpBarView),
            defaultValue: false,
            propertyChanged: OnEnColumnasChanged);

    public bool EnColumnas
    {
        get => (bool)GetValue(EnColumnasProperty);
        set => SetValue(EnColumnasProperty, value);
    }

    private static void OnEnColumnasChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not RfcCpBarView view) return;

        if ((bool)newValue)
        {
            view.RootGrid.ColumnDefinitions.Clear();
            view.RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            view.RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            view.RootGrid.ColumnSpacing = 8;
            Grid.SetColumn(view.BorderCp, 1);
            view.BorderRfc.HorizontalOptions = LayoutOptions.Fill;
            view.BorderCp.HorizontalOptions = LayoutOptions.Fill;
            view.LabelRfc.HorizontalTextAlignment = TextAlignment.Center;
            view.LabelCp.HorizontalTextAlignment = TextAlignment.Center;
        }
        else
        {
            view.RootGrid.ColumnDefinitions.Clear();
            view.RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            view.RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            view.RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            view.RootGrid.ColumnSpacing = 0;
            Grid.SetColumn(view.BorderCp, 2);
            view.BorderRfc.HorizontalOptions = LayoutOptions.Start;
            view.BorderCp.HorizontalOptions = LayoutOptions.End;
            view.LabelRfc.HorizontalTextAlignment = TextAlignment.Start;
            view.LabelCp.HorizontalTextAlignment = TextAlignment.Start;
        }
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
