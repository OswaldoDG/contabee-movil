namespace ContaBeeMovil.Pages;

public partial class MainTabbedPage : ContentPage
{
    private readonly DashboardPage _dashboardPage;
    private readonly FacturacionPage _facturacionPage;
    private View? _dashboardView;
    private View? _facturacionView;
    private int _currentIndex = -1;

    public MainTabbedPage(IServiceProvider services)
    {
        InitializeComponent();

        _dashboardPage = services.GetRequiredService<DashboardPage>();
        _facturacionPage = services.GetRequiredService<FacturacionPage>();

        _dashboardView = _dashboardPage.Content;
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

        await PageContainer.FadeTo(0, 120, Easing.CubicIn);

        switch (index)
        {
            case 0:
                PageContainer.Content = _dashboardView;
                PageContainer.BindingContext = _dashboardPage.BindingContext;
                _dashboardPage.OnTabActivated();
                break;

            case 1:
                PageContainer.Content = _facturacionView;
                PageContainer.BindingContext = _facturacionPage.BindingContext;
                _facturacionPage.OnTabActivated();
                break;
        }

        await PageContainer.FadeTo(1, 180, Easing.CubicOut);
    }
}