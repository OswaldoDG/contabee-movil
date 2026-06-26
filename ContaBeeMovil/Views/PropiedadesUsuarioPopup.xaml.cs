using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

public partial class PropiedadesUsuarioPopup : Popup
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioToast _toast;
    private readonly Guid _cfid;
    private readonly Guid _usuarioId;
    private readonly Func<Task>? _onAsociacionCambiada;
    private List<PropiedadUsuarioCF> _originales = [];
    private bool _valorOriginalCaptura;
    private bool _valorOriginalActiva;
    private bool _suprimirActiva;

    public PropiedadesUsuarioPopup(IServicioCrm servicioCrm, IServicioToast toast, AppState appState, Guid cfid, Guid usuarioId, string nombre, bool asociacionActiva, bool esPropio = false, Func<Task>? onAsociacionCambiada = null)
    {
        InitializeComponent();
        _servicioCrm          = servicioCrm;
        _toast                = toast;
        _cfid                 = cfid;
        _usuarioId            = usuarioId;
        _onAsociacionCambiada = onAsociacionCambiada;

        LblTitulo.Text    = nombre;
        BadgeYo.IsVisible = esPropio;

        _valorOriginalActiva   = asociacionActiva;
        SwitchActiva.IsToggled  = asociacionActiva;
        SwitchActiva.IsVisible  = !esPropio;

        if (appState.EsDev && esPropio)
        {
            var p = appState.Perfil;
            LblDebugInfo.Text  = $"[Sesion] EsLoginLess: {appState.EsLoginLess}\n" +
                                  $"[Perfil] DisplayName: {p?.DisplayName}\n" +
                                  $"[Perfil] Iniciales: {p?.Iniciales}\n" +
                                  $"[CF] CuentaFiscalId: {cfid}\n" +
                                  $"[Usuario] Id: {usuarioId}\n" +
                                  $"[Usuario] EsPropio: {esPropio}";
            PanelDebug.IsVisible = true;
        }

        var info = DeviceDisplay.MainDisplayInfo;
        double density = info.Density > 0 ? info.Density : 1;
        CardBorder.WidthRequest = (info.Width / density) - 40;

        SwitchActiva.Toggled  += OnSwitchActivaToggled;
        SwitchCaptura.Toggled += (_, _) => ActualizarControles();

        _ = CargarAsync();
    }

    private void ActualizarControles()
    {
        bool activa = SwitchActiva.IsToggled;
        SwitchCaptura.IsEnabled = activa;
        FilaCaptura.Opacity     = activa ? 1 : 0.4;
        BtnGuardar.IsEnabled    = activa && SwitchCaptura.IsToggled != _valorOriginalCaptura;
    }

    private async Task CargarAsync()
    {
        SetCargando(true);
        var resp = await _servicioCrm.GetPropiedadesUsuario(_cfid, _usuarioId);
        if (!resp.Ok)
        {
            bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
            await _toast.MostrarAsync(
                es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? "Error al cargar propiedades",
                ToastIcono.Error);
            await CloseAsync();
            return;
        }
        _originales = resp.Payload ?? [];
        _valorOriginalCaptura    = EsBoolTrue(Obtener("UsuarioCaptura"));
        SwitchCaptura.IsToggled  = _valorOriginalCaptura;
        SetCargando(false);
        ActualizarControles();
    }

    // El switch de asociación guarda al instante: muestra spinner, llama al backend,
    // refresca la lista de usuarios y mantiene abierto el popup.
    private async void OnSwitchActivaToggled(object? sender, ToggledEventArgs e)
    {
        if (_suprimirActiva) return;

        bool nuevoValor = e.Value;
        SetCargando(true, nuevoValor ? "Activando…" : "Desactivando…");
        var resp = await _servicioCrm.SetActivaAsociacion(_cfid, _usuarioId, nuevoValor);
        SetCargando(false);

        if (!resp.Ok)
        {
            bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
            await _toast.MostrarAsync(
                es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? "Error al actualizar la asociación",
                ToastIcono.Error);
            // Revertir el switch sin volver a disparar el handler
            _suprimirActiva = true;
            SwitchActiva.IsToggled = !nuevoValor;
            _suprimirActiva = false;
            ActualizarControles();
            return;
        }

        _valorOriginalActiva = nuevoValor;
        ActualizarControles();
        await _toast.MostrarAsync(nuevoValor ? "Usuario activado" : "Usuario desactivado", ToastIcono.Info);

        if (_onAsociacionCambiada is not null)
            await _onAsociacionCambiada();
    }

    private async void OnGuardar(object sender, EventArgs e)
    {
        if (SwitchCaptura.IsToggled == _valorOriginalCaptura)
        {
            await CloseAsync();
            return;
        }

        SetCargando(true, "Guardando…");
        try
        {
            var resp = await _servicioCrm.SetPropiedadUsuario(_cfid, _usuarioId, "UsuarioCaptura", SwitchCaptura.IsToggled ? "1" : "0");
            if (!resp.Ok)
            {
                bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
                await _toast.MostrarAsync(
                    es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? "Error al guardar",
                    ToastIcono.Error);
                return;
            }

            await _toast.MostrarAsync("Propiedades guardadas", ToastIcono.Info);
            await CloseAsync();
        }
        finally
        {
            SetCargando(false);
        }
    }

    private async void OnCancelar(object sender, EventArgs e) => await CloseAsync();

    private void SetCargando(bool cargando, string? texto = null)
    {
        Spinner.IsRunning      = cargando;
        SpinnerPanel.IsVisible = cargando;
        LblSpinner.Text        = texto ?? "";
        LblSpinner.IsVisible   = cargando && !string.IsNullOrEmpty(texto);
        FormContent.IsVisible  = !cargando;
        SwitchActiva.IsEnabled = !cargando;
        if (cargando) BtnGuardar.IsEnabled = false;
    }

    private string Obtener(string prop) =>
        _originales.FirstOrDefault(p => p.Propiedad == prop)?.ValorPropiedad ?? "";

    private static bool EsBoolTrue(string val) => val == "1";

}
