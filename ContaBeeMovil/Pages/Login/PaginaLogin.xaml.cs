using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Dev;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly IServicioModoDeveloper _modoDeveloper;
    private readonly IServicioToast _servicioToast;
    private int _tapCount = 0;

    public static bool LimpiarAlNavegar { get; set; }

    public PaginaLogin(LoginViewModel viewModel, IServicioModoDeveloper modoDeveloper, IServicioToast servicioToast)
    {
        InitializeComponent();
        this._viewModel = viewModel;
        this._modoDeveloper = modoDeveloper;
        this._servicioToast = servicioToast;
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

        if (LimpiarAlNavegar)
        {
            LimpiarAlNavegar = false;
            _viewModel.LimpiarCampos();
        }

        _tapCount = 0;
        LogsButton.IsVisible = AppState.Instance.EsDev;
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
            _modoDeveloper.Activar();
            LogsButton.IsVisible = true;
            await _servicioToast.MostrarAsync("Modo Desarrollador activado", ToastIcono.Info, ToastPosicion.Bottom);
            _tapCount = 0;
        }
    }

    private async void OnLogsClicked(object? sender, TappedEventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<LogsPage>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(page);
    }
}
