using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;
using System.Globalization;

namespace ContaBeeMovil.Pages.Login;

public partial class PaginaLogin : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServicioToast _servicioToast;
    private readonly IAppLogger _logger;
    private const string ClaveMododDev = "ModoDeveloper";
    private const int TapThreshold = 10;
    private readonly object _tapSync = new();
    private bool _modoDevActivo;
    private int _tapCount = 0;

    public static bool LimpiarAlNavegar { get; set; }

    public PaginaLogin(LoginViewModel viewModel, IServicioAlmacenamiento almacenamiento, IServicioToast servicioToast, IAppLogger logger)
    {
        InitializeComponent();
        this._viewModel = viewModel;
        this._almacenamiento = almacenamiento;
        this._servicioToast = servicioToast;
        this._logger = logger;
        BindingContext = this._viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _logger.Info("Login.ScreenOpened", "Pantalla de login mostrada.");

        FormContainer.Opacity = 1;
        FormContainer.TranslationX = 0;
        LogoImage.Opacity = 1;
        LogoImage.Scale = 1;
        HeaderContainer.IsVisible = true;

        if (LimpiarAlNavegar)
        {
            LimpiarAlNavegar = false;
            _viewModel.LimpiarCampos();
        }

        _modoDevActivo = AppState.Instance.EsDev;
        _tapCount = 0;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async void OnLogoTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var faltan = 0;
            var accion = string.Empty;
            var debeCambiarEstado = false;
            var nuevoEstado = false;

            lock (_tapSync)
            {
                _tapCount++;
                faltan = TapThreshold - _tapCount;

                if (faltan <= 0)
                {
                    _tapCount = 0;
                    _modoDevActivo = !_modoDevActivo;
                    nuevoEstado = _modoDevActivo;
                    debeCambiarEstado = true;
                }
                else
                {
                    accion = _modoDevActivo ? "desactivar" : "activar";
                }
            }

            if (faltan > 0)
            {
                _ = _servicioToast.MostrarAsync(
                    $"Faltan {faltan} toques para {accion} el modo desarrollador",
                    ToastIcono.Info,
                    ToastPosicion.Bottom,
                    duracionMs: 550,
                    reemplazarAnteriores: true);
                return;
            }

            if (debeCambiarEstado)
            {
                var debugLoggingEnabled = Preferences.Get("DebugLoggingEnabled", false);
                var dto = new ModoDeveloperDto
                {
                    EsDev = nuevoEstado,
                    FechaActivacion = DateTime.UtcNow.ToString("O"),
                    DebugLoggingEnabled = debugLoggingEnabled
                };

                await _almacenamiento.GuardarSeguroAsync(ClaveMododDev, dto);

                AppState.Instance.EsDev = nuevoEstado;

                if (nuevoEstado)
                {
                    await _servicioToast.MostrarAsync(
                        "Modo Desarrollador activado",
                        ToastIcono.Info,
                        ToastPosicion.Bottom,
                        duracionMs: 550,
                        reemplazarAnteriores: true);
                    _logger.Info("Login.DeveloperModeEnabled", "Modo desarrollador activado desde login.");
                }
                else
                {
                    await _servicioToast.MostrarAsync(
                        "Modo Desarrollador desactivado",
                        ToastIcono.Info,
                        ToastPosicion.Bottom,
                        duracionMs: 550,
                        reemplazarAnteriores: true);
                    _logger.Info("Login.DeveloperModeDisabled", "Modo desarrollador desactivado desde login.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.ToggleDeveloperModeException", "Excepción no controlada al alternar modo desarrollador.", ex);
            _ = _servicioToast.MostrarAsync("No se pudo actualizar el modo desarrollador.", ToastIcono.Warning, ToastPosicion.Bottom);
        }
    }
}
