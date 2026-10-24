using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages;

public partial class MainTabbedPage : ContentPage
{
    private readonly DashboardPage   _dashboardPage;
    private readonly FacturacionPage _facturacionPage;
    private readonly Devoluciones.PaginaDevoluciones _devolucionesPage;
    private readonly Comprobaciones.PaginaComprobaciones _comprobacionesPage;
    private readonly EquipoPage      _equipoPage;

    private View? _dashboardView;
    private View? _facturacionView;
    private View? _devolucionesView;
    private View? _comprobacionesView;
    private View? _equipoView;

    private int _currentIndex = -1;
    private bool _estabaDegradado;   // sin red O servicio caído
    private bool _eraSinRed;         // cuál de los dos, para el mensaje de recuperación
    private bool _reintentando;
    private bool _estabaActualizandoCF;

    internal static event Action? EquipoRequested;
    internal static event Action<CreditoGanado[]>? ResaltarCreditosRequested;

    /// <summary>
    /// Pide al dashboard mostrarse (tab 0) y resaltar las tarjetas de los créditos que aumentaron.
    /// Lo usa el flujo de compra tras una compra exitosa.
    /// </summary>
    internal static void SolicitarResaltarCreditos(params CreditoGanado[] creditos)
        => ResaltarCreditosRequested?.Invoke(creditos);

    public MainTabbedPage(IServiceProvider services)
    {
        InitializeComponent();

        _dashboardPage      = services.GetRequiredService<DashboardPage>();
        _facturacionPage    = services.GetRequiredService<FacturacionPage>();
        _devolucionesPage   = services.GetRequiredService<Devoluciones.PaginaDevoluciones>();
        _comprobacionesPage = services.GetRequiredService<Comprobaciones.PaginaComprobaciones>();
        _equipoPage         = services.GetRequiredService<EquipoPage>();

        _dashboardView      = _dashboardPage.Content;
        _facturacionView    = _facturacionPage.Content;
        _devolucionesView   = _devolucionesPage.Content;
        _comprobacionesView = _comprobacionesPage.Content;
        _equipoView         = _equipoPage.Content;

        MonthNavBar.BindingContext = _dashboardPage.BindingContext;
        _facturacionPage.Filtros.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Views.FiltrosFacturasView.PeriodoTexto) && _currentIndex == 1)
                LabelTitulo.Text = _facturacionPage.Filtros.PeriodoTexto;
        };

        _comprobacionesPage.Filtros.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Views.FiltrosComprobacionesView.PeriodoTexto) && _currentIndex == 3)
                LabelTitulo.Text = "Comprobaciones";
        };

        EquipoRequested += ForceEquipoTab;
        ResaltarCreditosRequested += OnResaltarCreditosRequested;

        _senalesModoCompleto = LeerSenalesModoCompleto();

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.EsLoginLess) ||
                e.PropertyName == nameof(AppState.CuentaFiscalActual))
                MainThread.BeginInvokeOnMainThread(ActualizarVisibilidadTabs);
            else if (e.PropertyName == nameof(AppState.ModoSimple))
                MainThread.BeginInvokeOnMainThread(OnModoSimpleCambiado);
            else if (e.PropertyName == nameof(AppState.Licenciamiento) ||
                     e.PropertyName == nameof(AppState.MisUsuarios) ||
                     e.PropertyName == nameof(AppState.CuentasFiscales))
                MainThread.BeginInvokeOnMainThread(EvaluarModoCompleto);
            else if (e.PropertyName == nameof(AppState.ModoOffline) ||
                     e.PropertyName == nameof(AppState.ServicioNoDisponible))
                MainThread.BeginInvokeOnMainThread(ActualizarModoOffline);
            else if (e.PropertyName == nameof(AppState.EstaActualizandoCF))
                MainThread.BeginInvokeOnMainThread(OnEstadoActualizacionCFCambiado);
            else if (e.PropertyName == nameof(AppState.MostrarCargaGlobal))
                MainThread.BeginInvokeOnMainThread(ActualizarOverlayCarga);
        };

        ActualizarVisibilidadTabs();
        ActualizarModoOffline();
        SwitchToTab(0);
    }

    private bool EquipoDebeEstarVisible() =>
        !AppState.Instance.ModoSimple &&
        !AppState.Instance.EsLoginLess &&
        AppState.Instance.CuentaFiscalActual?.TipoCuenta != TipoCuenta.Secundaria;

    private bool TabVisible(int index) => index switch
    {
        2 or 3 => !AppState.Instance.ModoSimple,
        4      => EquipoDebeEstarVisible(),
        _      => true,
    };

    private void ActualizarVisibilidadTabs()
    {
        for (int i = 2; i <= 4; i++)
            TabBar.SetTabVisible(i, TabVisible(i));

        if (_currentIndex >= 0 && !TabVisible(_currentIndex))
            SwitchToTab(0);
    }

    // ── Modo simple ───────────────────────────────────────────────────────────

    private void OnModoSimpleCambiado()
    {
        ActualizarVisibilidadTabs();

        if (AppState.Instance.ModoSimple)
            SwitchToTab(0);   // al activarlo se regresa al Inicio (con el fade normal de pestaña)
        else
            _ = PrecargarListadosAsync();   // las pestañas que reaparecen cargan en segundo plano
    }

    // Señales de que el usuario ya necesita el modo completo. Se comparan con las del último
    // vistazo: solo una señal NUEVA lo cambia, así quien regresa a simple a propósito no vuelve
    // a completo en cada recarga de la licencia o de los usuarios.
    private (bool Colaboracion, bool Equipo, bool Vinculado) _senalesModoCompleto;

    private static (bool Colaboracion, bool Equipo, bool Vinculado) LeerSenalesModoCompleto()
    {
        var estado = AppState.Instance;
        return (
            // Solo sirven para reembolsos y comprobaciones.
            (estado.Licenciamiento?.CreditosColabDisponibles ?? 0) > 0,
            // MisUsuarios incluye al propio usuario: más de uno = hay equipo en la cuenta.
            (estado.MisUsuarios?.Count ?? 0) > 1,
            // Vinculado a la cuenta de alguien más (p. ej. al terminar "Vincularme").
            estado.CuentasFiscales?.Any(c => c.TipoCuenta == TipoCuenta.Secundaria) == true);
    }

    private void EvaluarModoCompleto()
    {
        var actuales = LeerSenalesModoCompleto();
        var previas = _senalesModoCompleto;
        _senalesModoCompleto = actuales;

        if (!AppState.Instance.ModoSimple) return;

        string? motivo =
            actuales.Colaboracion && !previas.Colaboracion ? "para que uses tus créditos de Colaboración"
            : actuales.Vinculado && !previas.Vinculado     ? "porque te vinculaste a una cuenta"
            : actuales.Equipo && !previas.Equipo           ? "porque tu cuenta ya tiene equipo"
            : null;
        if (motivo is null) return;

        App.Services.GetRequiredService<IServicioLogs>().Info($"[ModoSimple] Cambio a modo completo: {motivo}");
        AppState.Instance.ModoSimple = false;
        _ = App.Services.GetRequiredService<IServicioToast>().MostrarAsync(
            $"Activamos el modo completo {motivo}. Puedes volver al modo simple desde el menú.",
            ToastIcono.Info, ToastPosicion.Bottom, 5000);
    }

    private void ActualizarOverlayCarga()
    {
        OverlayCargaGlobal.IsVisible = AppState.Instance.MostrarCargaGlobal;
    }

    private void ActualizarModoOffline()
    {
        var offline = AppState.Instance.ModoOffline;
        var servicioCaido = AppState.Instance.ServicioNoDisponible;
        var degradado = offline || servicioCaido;

        BannerOffline.IsVisible = degradado;

        // Si no hay red, el servicio "caído" es consecuencia y no causa: gana ese mensaje,
        // y no ofrecemos Reintentar porque no hay nada que reintentar contra.
        LabelBanner.Text = offline ? "Sin Conexión" : "No pudimos conectar con ContaBee";
        BtnReintentarBanner.IsVisible = servicioCaido && !offline;

        if (_estabaDegradado && !degradado)
        {
            var toast = App.Services.GetRequiredService<IServicioToast>();
            _ = toast.MostrarAsync(
                _eraSinRed ? "Conexión Restaurada" : "Servicio restablecido",
                ToastIcono.Info, ToastPosicion.Bottom);
        }

        _estabaDegradado = degradado;
        _eraSinRed = offline;
    }

    private async void OnReintentarBannerTapped(object? sender, TappedEventArgs e)
    {
        if (_reintentando) return;
        _reintentando = true;
        BtnReintentarBanner.Opacity = 0.4;

        try
        {
            // El usuario pide explícitamente reintentar: el interruptor no debe cortarle la
            // llamada por lo aprendido hace un rato.
            App.Services.GetRequiredService<InterruptorApi>().Reiniciar();

            // Recarga completa del estado de sesión (perfil, asociaciones, tarjetas, licencia).
            // Si el backend ya volvió, AuthHandler apagará ServicioNoDisponible al primer 2xx
            // y el banner se esconde solo.
            await App.Services.GetRequiredService<IServicioSesion>().PosLoginAsync();

            if (AppState.Instance.ServicioNoDisponible)
            {
                var toast = App.Services.GetRequiredService<IServicioToast>();
                await toast.MostrarAsync("El servicio sigue sin responder", ToastIcono.Warning, ToastPosicion.Bottom);
            }
        }
        catch (Exception ex)
        {
            App.Services.GetRequiredService<IServicioLogs>().Error($"[Banner] Reintento falló: {ex.Message}");
        }
        finally
        {
            BtnReintentarBanner.Opacity = 1;
            _reintentando = false;
        }
    }

    /// <summary>
    /// Detecta el fin de un cambio de cuenta fiscal activa (transición true → false de
    /// <see cref="AppState.EstaActualizandoCF"/>, momento en que la cuenta y sus datos —licencia,
    /// usuarios— ya están refrescados) e invalida los resultados cacheados de las pestañas de
    /// listado para que no muestren datos de la cuenta anterior.
    /// </summary>
    private void OnEstadoActualizacionCFCambiado()
    {
        var actualizando = AppState.Instance.EstaActualizandoCF;
        if (_estabaActualizandoCF && !actualizando)
            RefrescarListadosPorCambioDeCuenta();
        _estabaActualizandoCF = actualizando;
    }

    private void RefrescarListadosPorCambioDeCuenta()
    {
        // Las pestañas inactivas quedan marcadas para recargar al activarse; el Dashboard
        // ya se refresca por su propia suscripción a CuentaFiscalActual.
        _facturacionPage.InvalidarConsulta();
        _devolucionesPage.InvalidarConsulta();
        _comprobacionesPage.InvalidarConsulta();

        // La pestaña visible se recarga de inmediato.
        ActivarTabActual();
    }

    private async void OnResaltarCreditosRequested(CreditoGanado[] creditos)
    {
        if (_currentIndex != 0) SwitchToTab(0);   // pone Dashboard + fade de 140ms
        await Task.Delay(280);                     // deja asentar el fade antes de animar
        await _dashboardPage.ResaltarCreditosAsync(creditos);
        // El label quedó en el valor final con el binding roto (Text directo). Primero sincronizamos
        // con el servidor —sin efecto visual, el binding está roto— y SOLO DESPUÉS restauramos el
        // binding: así evalúa al valor real (= valor final) y no salta al valor viejo y de vuelta.
        await App.Services.GetRequiredService<IServicioSesion>().GetLicenciaAsync();
        _dashboardPage.RestaurarBindingsCreditos();
    }

    private void ForceEquipoTab()
    {
        if (!EquipoDebeEstarVisible()) return;
        _currentIndex = -1;
        SwitchToTab(4);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_currentIndex >= 0)
            ActivarTabActual();
        _ = PrecargarListadosAsync();
    }

    /// <summary>
    /// Mientras el usuario está en el Home, los listados piden sus datos con los filtros
    /// guardados. Si el API estaba dormido (Azure apaga los pods sin tráfico, ~15 s en
    /// despertar), esa espera pasa aquí y no cuando el usuario abre la pestaña. Cada página
    /// ignora la llamada si ya tiene datos, así que repetirla al volver de otra página es gratis.
    /// </summary>
    private async Task PrecargarListadosAsync()
    {
        // Deja pasar primero la consulta del Home y el primer pintado.
        await Task.Delay(1500);
        try
        {
            // En modo simple no se piden las pestañas ocultas.
            Task[] tareas = AppState.Instance.ModoSimple
                ? [_facturacionPage.PrecargarAsync()]
                : [_facturacionPage.PrecargarAsync(), _devolucionesPage.PrecargarAsync(), _comprobacionesPage.PrecargarAsync()];
            await Task.WhenAll(tareas);
        }
        catch (Exception ex)
        {
            App.Services.GetRequiredService<IServicioLogs>().Error($"[Precarga] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnTabChanged(object? sender, int index) => SwitchToTab(index);

    private async void SwitchToTab(int index)
    {
        if (!TabVisible(index)) return;
        if (_currentIndex == index) return;
        _currentIndex = index;

        TabBar.SelectedIndex = index;

        PageContainer.Opacity = 0;

        MonthNavBar.IsVisible = index == 0;
        LabelTitulo.IsVisible = index != 0;

        switch (index)
        {
            case 0:
                PageContainer.Content = _dashboardView;
                PageContainer.BindingContext = _dashboardPage.BindingContext;
                break;

            case 1:
                PageContainer.Content = _facturacionView;
                PageContainer.BindingContext = _facturacionPage.BindingContext;
                LabelTitulo.Text = _facturacionPage.Filtros.PeriodoTexto;
                break;

            case 2:
                PageContainer.Content = _devolucionesView;
                PageContainer.BindingContext = _devolucionesPage.BindingContext;
                LabelTitulo.Text = "Reembolsos";
                break;

            case 3:
                PageContainer.Content = _comprobacionesView;
                PageContainer.BindingContext = _comprobacionesPage.BindingContext;
                LabelTitulo.Text = "Comprobaciones";
                break;

            case 4:
                PageContainer.Content = _equipoView;
                PageContainer.BindingContext = _equipoPage.BindingContext;
                LabelTitulo.Text = "Equipo";
                break;
        }

        await PageContainer.FadeToAsync(1, 140, Easing.CubicOut);

        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            switch (index)
            {
                case 0: _dashboardPage.OnTabActivated(); break;
                case 1: _facturacionPage.OnTabActivated(); break;
                case 2: _devolucionesPage.OnTabActivated(); break;
                case 3: _comprobacionesPage.OnTabActivated(); break;
                case 4: _equipoPage.OnTabActivated(); break;
            }
        });
    }

    private void ActivarTabActual()
    {
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            switch (_currentIndex)
            {
                case 0: _dashboardPage.OnTabActivated(); break;
                case 1: _facturacionPage.OnTabActivated(); break;
                case 2: _devolucionesPage.OnTabActivated(); break;
                case 3: _comprobacionesPage.OnTabActivated(); break;
                case 4: _equipoPage.OnTabActivated(); break;
            }
        });
    }
}
