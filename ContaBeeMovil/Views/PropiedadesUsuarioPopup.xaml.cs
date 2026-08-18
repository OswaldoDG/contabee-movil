using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Views;

// Popup de propiedades de un usuario del equipo. Modelo de interaccion: todo se
// aplica al instante, como una pantalla de ajustes. No hay boton Guardar ni
// estado sucio; cada switch llama al backend al vuelo, muestra su propio
// indicador en la fila y se revierte si la llamada falla.
public partial class PropiedadesUsuarioPopup : Popup
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioToast _toast;
    private readonly IServicioAlerta _alerta;
    private readonly Guid _cfid;
    private readonly Guid _usuarioId;
    private readonly string _nombre;
    private readonly Func<Task>? _onAsociacionCambiada;
    private List<PropiedadUsuarioCF> _originales = [];
    private bool _suprimirEventos;   // evita re-disparar el handler al revertir un switch
    private bool _guardando;

    public PropiedadesUsuarioPopup(IServicioCrm servicioCrm, IServicioToast toast, IServicioAlerta alerta, AppState appState, Guid cfid, Guid usuarioId, string nombre, bool asociacionActiva, bool esPropio = false, Func<Task>? onAsociacionCambiada = null)
    {
        InitializeComponent();
        _servicioCrm          = servicioCrm;
        _toast                = toast;
        _alerta               = alerta;
        _cfid                 = cfid;
        _usuarioId            = usuarioId;
        _nombre               = nombre;
        _onAsociacionCambiada = onAsociacionCambiada;

        LblTitulo.Text    = nombre;
        BadgeYo.IsVisible = esPropio;

        SwitchActiva.IsToggled = asociacionActiva;
        FilaActiva.IsVisible   = !esPropio;
        ActualizarEtiquetaActiva();

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
        SwitchCaptura.Toggled += OnSwitchCapturaToggled;

        _ = CargarAsync();
    }

    // Un usuario inactivo no puede capturar: la fila de captura se bloquea y se
    // explica el porque en vez de solo atenuarla.
    // La etiqueta refleja el estado actual, no el nombre de la propiedad.
    private void ActualizarEtiquetaActiva() =>
        LblActiva.Text = SwitchActiva.IsToggled ? "Activo" : "Inactivo";

    private void ActualizarDependencias()
    {
        ActualizarEtiquetaActiva();
        bool activa = SwitchActiva.IsToggled;
        SwitchCaptura.IsEnabled   = activa && !_guardando;
        FilaCaptura.Opacity       = activa ? 1 : 0.4;
        LblAyudaCaptura.IsVisible = !activa && FilaActiva.IsVisible;
    }

    private async Task CargarAsync()
    {
        SetCargandoInicial(true);
        var resp = await _servicioCrm.GetPropiedadesUsuario(_cfid, _usuarioId);
        if (!resp.Ok)
        {
            await MostrarErrorAsync(resp, "Error al cargar propiedades");
            await CloseAsync();
            return;
        }
        _originales = resp.Payload ?? [];

        // El valor inicial no debe disparar un guardado
        _suprimirEventos        = true;
        SwitchCaptura.IsToggled = EsBoolTrue(Obtener("UsuarioCaptura"));
        _suprimirEventos        = false;

        SetCargandoInicial(false);
        ActualizarDependencias();
    }

    // Desactivar saca al colaborador de ESTA cuenta fiscal (no toca su cuenta
    // propia de ContaBee): es destructivo y se confirma antes. Activar no lo es.
    private async void OnSwitchActivaToggled(object? sender, ToggledEventArgs e)
    {
        if (_suprimirEventos) return;
        bool nuevoValor = e.Value;
        ActualizarEtiquetaActiva();   // sigue al switch, aun antes de confirmar

        if (!nuevoValor)
        {
            bool confirmado = await _alerta.MostrarAsync(
                "Desactivar colaborador",
                $"¿Desea desactivar a {_nombre}?",
                confirmarText: "Desactivar");
            if (!confirmado)
            {
                RevertirSwitch(SwitchActiva, true);
                return;
            }
        }

        SetGuardando(SpinnerActiva, true);
        var resp = await _servicioCrm.SetActivaAsociacion(_cfid, _usuarioId, nuevoValor);
        SetGuardando(SpinnerActiva, false);

        if (!resp.Ok)
        {
            await MostrarErrorAsync(resp, "Error al actualizar el colaborador");
            RevertirSwitch(SwitchActiva, !nuevoValor);
            return;
        }

        ActualizarDependencias();
        await _toast.MostrarAsync(nuevoValor ? "Colaborador activado" : "Colaborador desactivado", ToastIcono.Info);

        if (_onAsociacionCambiada is not null)
            await _onAsociacionCambiada();
    }

    private async void OnSwitchCapturaToggled(object? sender, ToggledEventArgs e)
    {
        if (_suprimirEventos) return;
        bool nuevoValor = e.Value;

        SetGuardando(SpinnerCaptura, true);
        var resp = await _servicioCrm.SetPropiedadUsuario(_cfid, _usuarioId, "UsuarioCaptura", nuevoValor ? "1" : "0");
        SetGuardando(SpinnerCaptura, false);

        if (!resp.Ok)
        {
            await MostrarErrorAsync(resp, "Error al guardar");
            RevertirSwitch(SwitchCaptura, !nuevoValor);
            return;
        }

        await _toast.MostrarAsync(nuevoValor ? "Captura habilitada" : "Captura deshabilitada", ToastIcono.Info);
    }

    private async void OnCerrar(object sender, EventArgs e) => await CloseAsync();

    private void RevertirSwitch(Switch control, bool valor)
    {
        _suprimirEventos   = true;
        control.IsToggled  = valor;
        _suprimirEventos   = false;
        ActualizarDependencias();
    }

    // Indicador en la propia fila: el formulario permanece visible y no salta.
    private void SetGuardando(ActivityIndicator indicador, bool guardando)
    {
        _guardando             = guardando;
        indicador.IsRunning    = guardando;
        indicador.Opacity      = guardando ? 1 : 0;
        SwitchActiva.IsEnabled = !guardando;
        ActualizarDependencias();
    }

    private void SetCargandoInicial(bool cargando)
    {
        Spinner.IsRunning      = cargando;
        SpinnerPanel.IsVisible = cargando;
        FormContent.IsVisible  = !cargando;
    }

    private async Task MostrarErrorAsync(Contabee.Api.Respuesta resp, string mensajeGenerico)
    {
        bool es403 = resp.Error?.HttpCode == System.Net.HttpStatusCode.Forbidden;
        await _toast.MostrarAsync(
            es403 ? "Solo el propietario puede gestionar permisos" : resp.Error?.Mensaje ?? mensajeGenerico,
            ToastIcono.Error);
    }

    private string Obtener(string prop) =>
        _originales.FirstOrDefault(p => p.Propiedad == prop)?.ValorPropiedad ?? "";

    private static bool EsBoolTrue(string val) => val == "1";

}
