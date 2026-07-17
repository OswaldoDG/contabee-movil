using ContaBeeMovil.Views;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;

namespace ContaBeeMovil.Pages.Equipo;

[QueryProperty(nameof(EsConCuentaParam), "esConCuenta")]
public partial class VincularPage : ContentPage
{
    private readonly VincularViewModel _viewModel;
    private Popup? _formularioPopup;

    public VincularPage(VincularViewModel viewModel)
    {
        InitializeComponent();
        _viewModel     = viewModel;
        BindingContext = viewModel;

        TokenBoxesGrid.GestureRecognizers.Add(
            new TapGestureRecognizer { Command = new Command(() => TokenEntry.Focus()) });

        _viewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(VincularViewModel.MostrarFormulario) && _viewModel.MostrarFormulario)
            {
                var popup = new VincularFormularioPopup(_viewModel);
                _formularioPopup = popup;
                await MainThread.InvokeOnMainThreadAsync(() => this.ShowPopupAsync(popup));
                _formularioPopup = null;
            }
        };

        _viewModel.VinculacionSinCuentaExitosa += async (_, _) =>
        {
            if (_formularioPopup is { } popup)
                await MainThread.InvokeOnMainThreadAsync(async () => await popup.CloseAsync(CancellationToken.None));
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("..");
            });
        };
    }

    public string EsConCuentaParam
    {
        set => _viewModel.EsConCuenta = bool.TryParse(value, out var b) && b;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.MostrarPasoUno)
        {
            // Se enfoca con un pequeño retraso, no durante el layout inicial: en iOS, abrir
            // el teclado antes de que la página termine de medirse deja un gap inferior que no
            // se revierte. Al esperar a que el layout se asiente, el teclado sube limpio.
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () => TokenEntry.Focus());
        }
    }
}
