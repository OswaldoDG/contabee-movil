using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IToastService _toastService;
    private const string ClaveMododDev = "ModoDeveloper";
    private int _tapCount = 0;

    public PaginaLogin(LoginViewModel viewModel, IServicioAlmacenamiento almacenamiento, IToastService toastService)
    {
        InitializeComponent();
        this._viewModel = viewModel;
        this._almacenamiento = almacenamiento;
        this._toastService = toastService;
        BindingContext = this._viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        FormContainer.Opacity = 1;
        FormContainer.TranslationX = 0;
        LogoImage.Opacity = 1;
        LogoImage.Scale = 1;
        HeaderContainer.IsVisible = true;

        EntryEmail.Focused += OnEntryFocused;
        EntryPassword.Focused += OnEntryFocused;
        EntryEmail.Unfocused += OnEntryUnfocused;
        EntryPassword.Unfocused += OnEntryUnfocused;

        _tapCount = 0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        EntryEmail.Focused -= OnEntryFocused;
        EntryPassword.Focused -= OnEntryFocused;
        EntryEmail.Unfocused -= OnEntryUnfocused;
        EntryPassword.Unfocused -= OnEntryUnfocused;
    }

    private void OnEntryFocused(object? sender, FocusEventArgs e)
    {
        HeaderContainer.IsVisible = false;
    }

    private void OnEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (!EntryEmail.IsFocused && !EntryPassword.IsFocused)
            HeaderContainer.IsVisible = true;
    }

    private async void OnLogoTapped(object? sender, TappedEventArgs e)
    {
        _tapCount++;

        if (_tapCount >= 10)
        {
            var dto = new ModoDeveloperDto
            {
                EsDev = true,
                FechaActivacion = DateTime.UtcNow.ToString("O")
            };
            await _almacenamiento.GuardarSeguroAsync(ClaveMododDev, dto);
            await _toastService.ShowAsync("Modo Desarrollador activado", ToastType.Success, position: ToastPosition.Bottom);
            _tapCount = 0;
        }
    }
}
