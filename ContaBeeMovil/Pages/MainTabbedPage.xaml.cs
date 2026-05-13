using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Pages;

public partial class MainTabbedPage : ContentPage
{
    private readonly DashboardPage   _dashboardPage;
    private readonly FacturacionPage _facturacionPage;
    private readonly EquipoPage      _equipoPage;

    // Guardadas por separado para sobrevivir el single-parent constraint de MAUI.
    // Al extraer Content antes de asignar parent, podemos reasignarla al volver al tab.
    private View? _dashboardView;
    private View? _facturacionView;
    private View? _equipoView;

    private int _currentIndex = -1;

    internal static event Action? EquipoRequested;

    public MainTabbedPage(IServiceProvider services)
    {
        InitializeComponent();

        _dashboardPage   = services.GetRequiredService<DashboardPage>();
        _facturacionPage = services.GetRequiredService<FacturacionPage>();
        _equipoPage      = services.GetRequiredService<EquipoPage>();

        _dashboardView   = _dashboardPage.Content;
        _facturacionView = _facturacionPage.Content;
        _equipoView      = _equipoPage.Content;

        MonthNavBar.BindingContext = _dashboardPage.BindingContext;
        // Sincronizar título cuando cambia el periodo en facturación
        _facturacionPage.Filtros.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Views.FiltrosFacturasView.PeriodoTexto) && _currentIndex == 1)
                LabelTitulo.Text = _facturacionPage.Filtros.PeriodoTexto;
        };

        EquipoRequested += ForceEquipoTab;

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.EsLoginLess))
                MainThread.BeginInvokeOnMainThread(ActualizarVisibilidadEquipo);
        };

        ActualizarVisibilidadEquipo();
        SwitchToTab(0);
    }

    private void ActualizarVisibilidadEquipo()
    {
        TabBar.SetEquipoVisible(!AppState.Instance.EsLoginLess);
    }

    private void ForceEquipoTab()
    {
        if (AppState.Instance.EsLoginLess) return;
        _currentIndex = -1;
        SwitchToTab(2);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Re-activa el tab actual al volver desde una página modal (TiendaPage, PaginaCaptura, etc.)
        if (_currentIndex >= 0)
            SwitchToTab(_currentIndex);
    }

    private void OnTabChanged(object? sender, int index) => SwitchToTab(index);

    private async void SwitchToTab(int index)
    {
        if (index == 2 && AppState.Instance.EsLoginLess) return;
        if (_currentIndex == index) return;
        _currentIndex = index;

        // 1) Actualizar indicador de pestaña
        TabBar.SelectedIndex = index;

        // 2) Swap directo del contenido — SIN fade-out (nada de espacio vacío)
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
                PageContainer.Content = _equipoView;
                PageContainer.BindingContext = _equipoPage.BindingContext;
                LabelTitulo.Text = "Equipo";
                break;
        }

        // 4) Fade-in suave del contenido nuevo
        await PageContainer.FadeToAsync(1, 140, Easing.CubicOut);

        // 5) Carga de datos diferida
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            switch (index)
            {
                case 0: _dashboardPage.OnTabActivated(); break;
                case 1: _facturacionPage.OnTabActivated(); break;
                case 2: _equipoPage.OnTabActivated(); break;
            }
        });
    }
}
