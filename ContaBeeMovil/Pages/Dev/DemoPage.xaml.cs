namespace ContaBeeMovil.Pages.Dev;

public partial class DemoPage : ContentPage
{
    private readonly DashboardPage   _dashboardPage;
    private readonly FacturacionPage _facturacionPage;

    private View? _dashboardView;
    private View? _facturacionView;

    private int _currentIndex = -1;

    public DemoPage(IServiceProvider services)
    {
        InitializeComponent();

        _dashboardPage   = services.GetRequiredService<DashboardPage>();
        _facturacionPage = services.GetRequiredService<FacturacionPage>();

        _dashboardView   = _dashboardPage.Content;
        _facturacionView = _facturacionPage.Content;

        SwitchToTab(0);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_currentIndex >= 0)
            SwitchToTab(_currentIndex);
    }

    private void OnTabChanged(object? sender, int index) => SwitchToTab(index);

    private async void SwitchToTab(int index)
    {
        if (_currentIndex == index) return;
        _currentIndex = index;

        TabBar.SelectedIndex = index;

        PageContainer.Opacity = 0;

        switch (index)
        {
            case 0:
                PageContainer.Content        = _dashboardView;
                PageContainer.BindingContext = _dashboardPage.BindingContext;
                break;
            case 1:
                PageContainer.Content        = _facturacionView;
                PageContainer.BindingContext = _facturacionPage.BindingContext;
                break;
        }

        await PageContainer.FadeTo(1, 140, Easing.CubicOut);

        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            switch (index)
            {
                case 0: _dashboardPage.OnTabActivated();   break;
                case 1: _facturacionPage.OnTabActivated(); break;
            }
        });
    }
}
