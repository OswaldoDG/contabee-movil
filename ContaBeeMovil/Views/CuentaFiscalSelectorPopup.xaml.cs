using CommunityToolkit.Maui.Views;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace ContaBeeMovil.Views;

public partial class CuentaFiscalSelectorPopup : Popup
{
    private bool _refrescando;

    public CuentaFiscalSelectorPopup()
    {
        InitializeComponent();
        ActualizarToggle();
        ConstruirLista();
    }

    // Refresco manual: re-consulta las asociaciones y reconcilia la selección (conserva la
    // seleccionada si sigue en la lista, si no toma la primera, si no queda ninguna → modo
    // limitado). Luego reconstruye la lista del popup con los datos frescos.
    private async void OnRefrescar(object? sender, TappedEventArgs e)
    {
        if (_refrescando) return;
        _refrescando = true;
        IconoRefrescar.IsVisible = false;
        SpinnerRefrescar.IsVisible = true;

        try
        {
            var sesion = IPlatformApplication.Current?.Services.GetRequiredService<IServicioSesion>();
            bool ok = sesion is not null && await sesion.RefrescarCuentasFiscalesAsync();

            if (ok)
                ConstruirLista();
            else
                await MostrarToastAsync("No se pudo actualizar. Revisa tu conexión.");
        }
        finally
        {
            SpinnerRefrescar.IsVisible = false;
            IconoRefrescar.IsVisible = true;
            _refrescando = false;
        }
    }

    private static async Task MostrarToastAsync(string mensaje)
    {
        var toast = IPlatformApplication.Current?.Services.GetService<IServicioToast>();
        if (toast is not null)
            await toast.MostrarAsync(mensaje, ToastIcono.Error);
    }

    private string GetTextoDisplay(AsociacionCuentaFiscalCompleta cuenta)
    {
        if (AppState.Instance.MostrarNombreFiscal)
        {
            var nombre = cuenta.CuentaFiscal?.Nombre;
            return string.IsNullOrWhiteSpace(nombre) ? "?" : UIHelpers.ToPascalCase(nombre);
        }
        return string.IsNullOrWhiteSpace(cuenta.Rfc) ? "?" : cuenta.Rfc;
    }

    private void ConstruirLista()
    {
        PanelLista.Children.Clear();

        var cuentas = AppState.Instance.CuentasFiscales;

        if (cuentas == null || cuentas.Count == 0)
        {
            PanelLista.Children.Add(CrearEstadoVacio());
            return;
        }

        var actual = AppState.Instance.CuentaFiscalActual;

        foreach (var cuenta in cuentas)
        {
            bool esActual = actual != null && cuenta.CuentaFiscalId == actual.CuentaFiscalId;
            PanelLista.Children.Add(UIHelpers.CrearItemSeleccionable(
                GetTextoDisplay(cuenta), esActual, () => OnSeleccionarCuenta(cuenta)));
        }
    }

    private VerticalStackLayout CrearEstadoVacio()
    {
        var label = new Label
        {
            Text              = "Aún no tienes cuentas fiscales registradas.",
            TextColor         = UIHelpers.GetColor("SecondaryText"),
            FontSize          = 14,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
        };

        var boton = new Button
        {
            Text            = "Registrar cuenta fiscal",
            BackgroundColor = UIHelpers.GetColor("Primary"),
            TextColor       = UIHelpers.GetColor("PrimaryText"),
            FontAttributes  = FontAttributes.Bold,
            CornerRadius    = 12,
            HeightRequest   = 48,
        };

        boton.Clicked += async (_, _) =>
        {
            await CloseAsync();
            await Shell.Current.GoToAsync(nameof(RegistrarRFCsPage));
        };

        return new VerticalStackLayout
        {
            Spacing = 16,
            Children = { label, boton }
        };
    }

    private async void OnSeleccionarCuenta(AsociacionCuentaFiscalCompleta cuenta)
    {
        AppState.Instance.CuentaFiscalActual  = cuenta;
        AppState.Instance.EstaActualizandoCF  = true;
        await CloseAsync();

        var sesion = IPlatformApplication.Current?.Services.GetRequiredService<IServicioSesion>();
        if (sesion is not null)
        {
            try
            {
                await sesion.GetLicenciaAsync();
                await sesion.GetMisUsuariosAsync();
            }
            catch { /* errores de red no deben impedir el cierre del popup */ }
        }

        AppState.Instance.EstaActualizandoCF = false;
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

        BgRfc.BackgroundColor    = nombre ? Colors.Transparent : UIHelpers.GetColor("Primary");
        LblRfc.TextColor         = nombre ? UIHelpers.GetColor("SecondaryText") : UIHelpers.GetColor("OnPrimary");
        LblRfc.FontAttributes    = nombre ? FontAttributes.None : FontAttributes.Bold;

        BgNombre.BackgroundColor = nombre ? UIHelpers.GetColor("Primary") : Colors.Transparent;
        LblNombre.TextColor      = nombre ? UIHelpers.GetColor("OnPrimary") : UIHelpers.GetColor("SecondaryText");
        LblNombre.FontAttributes = nombre ? FontAttributes.Bold : FontAttributes.None;
    }
}
