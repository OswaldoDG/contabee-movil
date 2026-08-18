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

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.EsLoginLess) ||
                e.PropertyName == nameof(AppState.CuentaFiscalActual))
                MainThread.BeginInvokeOnMainThread(ActualizarVisibilidadEquipo);
            else if (e.PropertyName == nameof(AppState.ModoOffline) ||
                     e.PropertyName == nameof(AppState.ServicioNoDisponible))
                MainThread.BeginInvokeOnMainThread(ActualizarModoOffline);
            else if (e.PropertyName == nameof(AppState.EstaActualizandoCF))
                MainThread.BeginInvokeOnMainThread(OnEstadoActualizacionCFCambiado);
            else if (e.PropertyName == nameof(AppState.MostrarCargaGlobal))
                MainThread.BeginInvokeOnMainThread(ActualizarOverlayCarga);
        };

        ActualizarVisibilidadEquipo();
        ActualizarModoOffline();
        SwitchToTab(0);
    }

    private bool EquipoDebeEstarVisible() =>
        !AppState.Instance.EsLoginLess &&
        AppState.Instance.CuentaFiscalActual?.TipoCuenta != TipoCuenta.Secundaria;

    private void ActualizarVisibilidadEquipo()
    {
        var visible = EquipoDebeEstarVisible();
        TabBar.SetEquipoVisible(visible);
        if (!visible && _currentIndex == 4)
            SwitchToTab(0);
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
    }

    private void OnTabChanged(object? sender, int index) => SwitchToTab(index);

    private async void SwitchToTab(int index)
    {
        if (index == 4 && !EquipoDebeEstarVisible()) return;
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
