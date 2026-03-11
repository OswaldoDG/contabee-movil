using Contabee.Api.abstractions;
using ContaBeeMovil.Services;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public PaginaLogin( LoginViewModel viewModel)
    {
        InitializeComponent();
        this._viewModel = viewModel;
        BindingContext = this._viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reset form animation state when page appears
        FormContainer.Opacity = 1;
        FormContainer.TranslationX = 0;
        LogoImage.Opacity = 1;
        LogoImage.Scale = 1;
    }
}
