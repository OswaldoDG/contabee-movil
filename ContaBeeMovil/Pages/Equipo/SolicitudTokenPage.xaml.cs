namespace ContaBeeMovil.Pages.Equipo;

[QueryProperty(nameof(EnSesionParam), "enSesion")]
public partial class SolicitudTokenPage : ContentPage
{
    private readonly SolicitudTokenViewModel _viewModel;

    public SolicitudTokenPage(SolicitudTokenViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;
    }

    public string EnSesionParam
    {
        set => _enSesion = bool.TryParse(value, out var b) && b;
    }

    private bool _enSesion;

    public bool EnSesion
    {
        get => _enSesion;
        set => _enSesion = value;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.IniciarAsync(_enSesion);
    }
}
