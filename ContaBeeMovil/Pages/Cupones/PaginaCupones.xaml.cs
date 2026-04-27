namespace ContaBee.Pages.Cupones;

public partial class PaginaCupones : ContentPage
{
    private readonly PaginaCuponesViewModel _viewModel;

    public PaginaCupones(PaginaCuponesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarCuponesAsync();
    }
}