using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using Contabee.Api.Logging;
using System.Windows.Input;

namespace ContaBeeMovil.Pages.Perfil;

public partial class RFCsPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private readonly IAppLogger _logger;
    private bool _autoNavegado;

    public RFCsPage(IServicioCrm servicioCrm, IServicioAlerta servicioAlerta, IServicioLogs logs, IAppLogger logger)
    {
        InitializeComponent();
        _servicioCrm = servicioCrm;
        _servicioAlerta = servicioAlerta;
        _logs = logs;
        _logger = logger;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Esperar a que termine la animación de navegación antes de poblar la lista.
        // Sin esto, Android deja un artifact gris encima del CollectionView.
        await Task.Delay(250);

        CargarCuentas();

        var cuentas = AppState.Instance.CuentasFiscales;
        if (!_autoNavegado && cuentas != null && cuentas.Count == 0)
        {
            _autoNavegado = true;
            await AbrirRegistrar();
        }
    }

    private void CargarCuentas()
    {
        var cuentas = AppState.Instance.CuentasFiscales ?? new List<AsociacionCuentaFiscalCompleta>();
        ListaCuentas.ItemsSource = cuentas.Select(c => new CuentaItem
        {
            Nombre = c.DireccionesFiscales?.FirstOrDefault()?.CuentaFiscal?.Nombre ?? string.Empty,
            Rfc = c.Rfc ?? "—",
            Regimen = ObtenerDescripcionRegimen(c.ClaveRegimenFiscal),
            DeleteCommand = new Command(async () => await ConfirmarEliminar(c))
        }).ToList();
    }

    private static string ObtenerDescripcionRegimen(string? clave)
    {
        if (string.IsNullOrEmpty(clave)) return "Sin régimen";
        var regimen = RegimenFiscalProvider.GetRegimenFiscal(null)
            .FirstOrDefault(r => r.Codigo == clave);
        return regimen?.Descripcion ?? clave;
    }

    private async void BtnAgregar_Clicked(object? sender, EventArgs e)
    {
        try { await AbrirRegistrar(); }
        catch (Exception ex)
        {
            _logs.Log($"[RFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "Ocurrió un error inesperado.", verBotonCancelar: false, confirmarText: "OK");
        }
    }

    private async Task AbrirRegistrar()
    {
        await Shell.Current.GoToAsync(nameof(RegistrarRFCsPage));
    }

    private async Task ConfirmarEliminar(AsociacionCuentaFiscalCompleta cuenta)
    {
        bool confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar",
            "Eliminar la cuenta fiscal es un proceso irreversible ¿Desea continuar?",
            confirmarText: "Si", cancelarText: "Cancelar");

        if (!confirmar) return;

        SetLoading(true);
        _logger.Info("Rfcs.Eliminar", "Inicio de eliminación de cuenta fiscal.");
        try
        {
            Contabee.Api.Respuesta respuesta;
            if (cuenta.TipoCuenta == TipoCuenta.Primaria)
                respuesta = await _servicioCrm.EliminarCuentaFiscal(cuenta.CuentaFiscalId.ToString());
            else
                respuesta = await _servicioCrm.EliminarAsociacionFiscal(cuenta.Id);

            if (respuesta.Ok)
            {
                _logger.Info("Rfcs.EliminarExitoso", "Cuenta fiscal eliminada correctamente.");
                var actualizadas = await _servicioCrm.GetAsociacionesFiscales();
                if (actualizadas.Ok && actualizadas.Payload != null)
                    AppState.Instance.CuentasFiscales = actualizadas.Payload;

                CargarCuentas();
                return;
            }

            _logger.Debug("Rfcs.EliminarError", "La API devolvió error al eliminar cuenta fiscal.", new Dictionary<string, object?>
            {
                ["Codigo"] = respuesta.Error?.Codigo,
                ["Mensaje"] = respuesta.Error?.Mensaje,
                ["HttpCode"] = (int?)respuesta.Error?.HttpCode
            });
            var mensaje = respuesta.Error?.Mensaje ?? "Error al eliminar la cuenta fiscal.";
            await _servicioAlerta.MostrarAsync("Error", mensaje, verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logger.Debug("Rfcs.EliminarException", "Excepción no controlada al eliminar cuenta fiscal.", ex);
            _logs.Log($"[RFCsPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "Error al eliminar la cuenta fiscal.", verBotonCancelar: false, confirmarText: "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        BtnAgregar.IsEnabled = !isLoading;
    }

    private class CuentaItem
    {
        public string Nombre { get; init; } = "";
        public string Rfc { get; init; } = "";
        public string Regimen { get; init; } = "";
        public ICommand DeleteCommand { get; init; } = null!;
        public bool TieneNombre => !string.IsNullOrWhiteSpace(Nombre);
    }
}
