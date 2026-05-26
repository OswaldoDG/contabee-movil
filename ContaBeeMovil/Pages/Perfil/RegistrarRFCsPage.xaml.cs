using ContaBeeMovil.Pages.Camara;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using Contabee.Api.Logging;

namespace ContaBeeMovil.Pages.Perfil;

[QueryProperty(nameof(FromLogin), "fromLogin")]
public partial class RegistrarRFCsPage : ContentPage
{
    public bool FromLogin
    {
        set => BtnVolverInicio.IsVisible = value;
    }

    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private readonly IServicioSesion _servicioSesion;
    private readonly IAppLogger _logger;

    public RegistrarRFCsPage(IServicioAlerta servicioAlerta, IServicioLogs logs, IServicioSesion servicioSesion, IAppLogger logger)
    {
        InitializeComponent();
        _servicioAlerta = servicioAlerta;
        _logs = logs;
        _servicioSesion = servicioSesion;
        _logger = logger;
    }

    private async void IconManual_Tapped(object? sender, EventArgs e)
    {
        try
        {
            _logger.Info("RegistrarRfcs.AbrirManual", "Inicio de apertura de pantalla de registro manual.");
            var page = MauiProgram.Services.GetService(typeof(ManualRegistroPage)) as Page;
            if (page != null)
            {
                await Navigation.PushModalAsync(page);
                _logger.Info("RegistrarRfcs.AbrirManualExitoso", "Pantalla de registro manual abierta correctamente.");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("RegistrarRfcs.AbrirManualException", "Excepción no controlada al abrir registro manual.", ex);
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el registro manual.", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async void IconQr_Tapped(object? sender, EventArgs e)
    {
        try
        {
            _logger.Info("RegistrarRfcs.AbrirQr", "Inicio de apertura de pantalla de escaneo QR.");
            var qrPage = MauiProgram.Services.GetService(typeof(QRPage)) as Page;
            if (qrPage != null)
            {
                await Navigation.PushModalAsync(qrPage);
                _logger.Info("RegistrarRfcs.AbrirQrExitoso", "Pantalla de escaneo QR abierta correctamente.");
            }
            else
            {
                _logger.Debug("RegistrarRfcs.AbrirQrNoDisponible", "No se encontró instancia de QRPage para navegación.");
                await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo de QR.", verBotonCancelar: false, confirmarText: "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("RegistrarRfcs.AbrirQrException", "Excepción no controlada al abrir escaneo QR.", ex);
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo abrir el escaneo QR.", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async void IconVincular_Tapped(object? sender, EventArgs e)
    {
        try
        {
            _logger.Info("RegistrarRfcs.Vincular", "Acción de vincular seleccionada (funcionalidad pendiente).");
            await _servicioAlerta.MostrarAsync("Vincular", "Funcionalidad para vincular próximamente.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logger.Debug("RegistrarRfcs.VincularException", "Excepción no controlada al mostrar mensaje de vincular.", ex);
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async void VolverAInicio_Clicked(object? sender, EventArgs e)
    {
        try
        {
            _logger.Info("RegistrarRfcs.VolverInicio", "Inicio de cierre de sesión desde RegistrarRFCs.");
            await _servicioSesion.CerrarSesionAsync();
            _logger.Info("RegistrarRfcs.VolverInicioExitoso", "Cierre de sesión completado desde RegistrarRFCs.");
        }
        catch (Exception ex)
        {
            _logger.Debug("RegistrarRfcs.VolverInicioException", "Excepción no controlada al cerrar sesión desde RegistrarRFCs.", ex);
            _logs.Log($"[RegistrarRFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudo cerrar sesión en este momento.", verBotonCancelar: false, confirmarText: "OK");
        }
    }
}
