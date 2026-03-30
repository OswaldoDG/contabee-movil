using ContaBeeMovil.Pages.Dashboard;

namespace ContaBeeMovil.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }

    public async void OnTabActivated() => await _viewModel.LoadDataAsync();
}
