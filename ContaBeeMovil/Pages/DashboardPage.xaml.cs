using ContaBeeMovil.Pages.Dashboard;

namespace ContaBeeMovil.Pages;

public partial class DashboardPage : ContentPage
{
    internal static bool PendienteActualizar { get; set; }

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
        bool forzar = PendienteActualizar;
        PendienteActualizar = false;
        await _viewModel.LoadDataAsync(forzar);
    }

    public async void OnTabActivated()
    {
        await Task.Yield(); // libera el hilo de UI inmediatamente
        bool forzar = PendienteActualizar;
        PendienteActualizar = false;
        await _viewModel.LoadDataAsync(forzar);
    }
}
