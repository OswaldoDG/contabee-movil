using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Logging;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServicioToast _servicioToast;
    private readonly IAppLogger _logger;
    private readonly LogContextService _logContextService;
    private const string ClaveMododDev = "ModoDeveloper";
    private int _tapCount = 0;

    public static bool LimpiarAlNavegar { get; set; }

    public PaginaLogin(LoginViewModel viewModel, IServicioAlmacenamiento almacenamiento, IServicioToast servicioToast, IAppLogger logger, LogContextService logContextService)
    {
        InitializeComponent();
        this._viewModel = viewModel;
        this._almacenamiento = almacenamiento;
        this._servicioToast = servicioToast;
        this._logger = logger;
        this._logContextService = logContextService;
        BindingContext = this._viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _logger.Info("Login.ScreenOpened", "Pantalla de login mostrada.", _logContextService.BuildCommonContext("PaginaLogin"));

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
        _logger.Debug("Login.LogoTapped", "Logo de login presionado.", _logContextService.BuildCommonContext("PaginaLogin"));

        if (_tapCount >= 10)
        {
            var dto = new ModoDeveloperDto
            {
                EsDev = true,
                FechaActivacion = DateTime.UtcNow.ToString("O")
            };
            await _almacenamiento.GuardarSeguroAsync(ClaveMododDev, dto);
            await _servicioToast.MostrarAsync("Modo Desarrollador activado", ToastIcono.Info, ToastPosicion.Bottom);
            _logger.Info("Login.DeveloperModeEnabled", "Modo desarrollador activado desde login.", _logContextService.BuildCommonContext("PaginaLogin"));
            _tapCount = 0;
        }
    }
}
