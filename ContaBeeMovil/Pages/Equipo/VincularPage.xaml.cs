namespace ContaBeeMovil.Pages.Equipo;

[QueryProperty(nameof(EsConCuentaParam), "esConCuenta")]
public partial class VincularPage : ContentPage
{
    private readonly VincularViewModel _viewModel;

    public VincularPage(VincularViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;

        TokenBoxesGrid.GestureRecognizers.Add(
            new TapGestureRecognizer { Command = new Command(() => TokenEntry.Focus()) });

    }

    public string EsConCuentaParam
    {
        set => _viewModel.EsConCuenta = bool.TryParse(value, out var b) && b;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.MostrarPasoUno)
            Dispatcher.Dispatch(() => TokenEntry.Focus());
    }
}
