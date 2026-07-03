using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;

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
            else if (e.PropertyName == nameof(AppState.EstaActualizandoCF))
                MainThread.BeginInvokeOnMainThread(ActualizarSpinner);
        };
    }

    private void ActualizarSpinner()
    {
        bool actualizando     = AppState.Instance.EstaActualizandoCF;
        SpinnerRfc.IsRunning  = actualizando;
        SpinnerRfc.IsVisible  = actualizando;
        LabelRfcCp.IsVisible  = !actualizando;
    }

    private void ActualizarDatos()
    {
        // Sin cuenta fiscal activa → mostramos el estado "sin cuenta" (aviso + Actualizar)
        // en lugar del selector de RFC/CP.
        bool sinCuenta = AppState.Instance.CuentaFiscalActual is null;
        RootGrid.IsVisible = !sinCuenta;
        SinCuentaBar.IsVisible = sinCuenta;
        if (sinCuenta) return;

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
            var nombre = cuenta.CuentaFiscal?.Nombre;
            return string.IsNullOrWhiteSpace(nombre) ? "?" : UIHelpers.ToPascalCase(nombre);
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

    private bool _actualizandoAcceso;
    private async void OnActualizarAccesoTapped(object? sender, TappedEventArgs e)
    {
        if (_actualizandoAcceso) return;
        _actualizandoAcceso = true;
        try
        {
            var sesion = App.Services.GetRequiredService<IServicioSesion>();
            var recuperado = await sesion.RefrescarAccesoAsync();

            // Si recuperó, RefrescarAccesoAsync ya reinició a un AppShell completo; si no,
            // seguimos sin cuentas y avisamos.
            if (!recuperado)
            {
                var toast = App.Services.GetRequiredService<IServicioToast>();
                await toast.MostrarAsync("Aún no tienes cuentas fiscales disponibles", ToastIcono.Warning, ToastPosicion.Bottom);
            }
        }
        finally
        {
            _actualizandoAcceso = false;
        }
    }
}
