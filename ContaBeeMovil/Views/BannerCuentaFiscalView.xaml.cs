using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace ContaBeeMovil.Views;

/// <summary>
/// Banner global que aparece cuando no hay una cuenta fiscal activa. Es autocontenido: se
/// suscribe a <see cref="AppState"/> y decide su propia visibilidad, así que basta con colocarlo
/// en el XAML de cualquier lugar que requiera cuenta fiscal. Distingue tres casos:
/// <list type="bullet">
///   <item>Loginless → "Actualizar" (intenta recuperar acceso vía <see cref="IServicioSesion.RefrescarAccesoAsync"/>).</item>
///   <item>Tiene cuentas pero ninguna activa → "Seleccionar" (abre el selector de cuenta).</item>
///   <item>No tiene cuentas registradas → "Registrar" (navega a <see cref="RegistrarRFCsPage"/>).</item>
/// </list>
/// En páginas que ya incluyen la barra RFC (<see cref="RfcCpBarView"/>) el banner se muestra dentro
/// de ella; en las que no, se coloca suelto (Tienda, Cupones, detalles).
/// </summary>
public partial class BannerCuentaFiscalView : ContentView
{
    private bool _procesandoTap;

    public BannerCuentaFiscalView()
    {
        InitializeComponent();
        Actualizar();

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.CuentaFiscalActual)
                               or nameof(AppState.CuentasFiscales)
                               or nameof(AppState.EsLoginLess))
                MainThread.BeginInvokeOnMainThread(Actualizar);
        };
    }

    private static bool TieneCuentasDisponibles() =>
        AppState.Instance.CuentasFiscales?.Count > 0;

    private void Actualizar()
    {
        bool sinCuenta = AppState.Instance.CuentaFiscalActual is null;
        IsVisible = sinCuenta;
        if (!sinCuenta) return;

        if (AppState.Instance.EsLoginLess)
        {
            LabelMensaje.Text = "No tienes cuentas fiscales disponibles";
            LabelAccion.Text = "Actualizar";
        }
        else if (TieneCuentasDisponibles())
        {
            LabelMensaje.Text = "No tienes una cuenta fiscal activa";
            LabelAccion.Text = "Seleccionar";
        }
        else
        {
            LabelMensaje.Text = "No tienes cuentas fiscales registradas";
            LabelAccion.Text = "Registrar";
        }
    }

    private async void OnBannerTapped(object? sender, TappedEventArgs e)
    {
        if (_procesandoTap) return;
        _procesandoTap = true;
        try
        {
            if (AppState.Instance.EsLoginLess)
            {
                await RefrescarAccesoAsync();
            }
            else if (TieneCuentasDisponibles())
            {
                var page = Shell.Current as Page ?? Application.Current!.Windows[0].Page!;
                await page.ShowPopupAsync(new CuentaFiscalSelectorPopup());
            }
            else
            {
                await Shell.Current.GoToAsync(nameof(RegistrarRFCsPage));
            }
        }
        finally
        {
            _procesandoTap = false;
        }
    }

    private static async Task RefrescarAccesoAsync()
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
}
