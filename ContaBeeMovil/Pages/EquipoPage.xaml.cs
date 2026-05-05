using System.ComponentModel;
using ContaBeeMovil.Pages.Equipo;

namespace ContaBeeMovil.Pages;

public partial class EquipoPage : ContentPage
{
    private readonly EquipoViewModel _viewModel;

    public EquipoPage(EquipoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        SubBotonesPanel.AnchorX = 1;
        SubBotonesPanel.AnchorY = 1;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EquipoViewModel.FabExpandido)) return;
        await AnimarSubBotones(_viewModel.FabExpandido);
    }

    private async Task AnimarSubBotones(bool mostrar)
    {
        if (mostrar)
        {
            SubBotonesPanel.IsVisible   = true;
            SubBotonesPanel.Opacity     = 0;
            SubBotonesPanel.Scale       = 0.88;
            SubBotonesPanel.TranslationY = 16;
            await Task.WhenAll(
                SubBotonesPanel.FadeTo(1, 200, Easing.CubicOut),
                SubBotonesPanel.ScaleTo(1.0, 200, Easing.CubicOut),
                SubBotonesPanel.TranslateTo(0, 0, 200, Easing.CubicOut)
            );
        }
        else
        {
            await Task.WhenAll(
                SubBotonesPanel.FadeTo(0, 150, Easing.CubicIn),
                SubBotonesPanel.ScaleTo(0.88, 150, Easing.CubicIn),
                SubBotonesPanel.TranslateTo(0, 16, 150, Easing.CubicIn)
            );
            SubBotonesPanel.IsVisible    = false;
            SubBotonesPanel.TranslationY = 0;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarAsync();
    }

    public async void OnTabActivated()
    {
        await Task.Yield();
        await _viewModel.CargarAsync();
    }
}
