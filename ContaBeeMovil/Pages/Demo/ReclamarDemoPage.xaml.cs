using Contabee.Api.abstractions;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using System.Net;

namespace ContaBeeMovil.Pages.Demo;

public partial class ReclamarDemoPage : ContentPage
{
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioSesion _servicioSesion;

    public ReclamarDemoPage(IServicioCrm servicioCrm, IServicioSesion servicioSesion)
    {
        InitializeComponent();
        _servicioCrm    = servicioCrm;
        _servicioSesion = servicioSesion;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EjecutarFlujoAsync();
    }

    private async Task EjecutarFlujoAsync()
    {
        var cuenta = AppState.Instance.CuentaFiscalActual;
        if (cuenta is null) return;

        MostrarEstado(Estado.Cargando);

        var rfc           = cuenta.Rfc;
        var cfid          = cuenta.CuentaFiscalId;
        var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();

        // ── Paso 1: Solicitar token ──────────────────────────────────────────
        var solicitud = await _servicioCrm.SolicitarLicenciamientoDemo(rfc, dispositivoId, cfid);

        if (!solicitud.Ok)
        {
            var mensaje = solicitud.HttpCode == HttpStatusCode.Conflict
                ? $"Los créditos para {rfc} ya fueron reclamados anteriormente."
                : "No se pudieron activar los créditos. Intenta de nuevo.";
            MostrarEstado(Estado.Error, mensaje);
            return;
        }

        var token = solicitud.Payload?.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            MostrarEstado(Estado.Error, "No se pudieron activar los créditos. Intenta de nuevo.");
            return;
        }

        // ── Paso 2: Activar con el token ─────────────────────────────────────
        var activacion = await _servicioCrm.ActivarLicenciamientoDemo(token, dispositivoId, cfid);

        if (!activacion.Ok)
        {
            var mensaje = activacion.HttpCode == HttpStatusCode.Conflict
                ? $"Los créditos para {rfc} ya fueron reclamados anteriormente."
                : "No se pudieron activar los créditos. Intenta de nuevo.";
            MostrarEstado(Estado.Error, mensaje);
            return;
        }

        // ── Éxito: refrescar licenciamiento en AppState ──────────────────────
        await _servicioSesion.GetLicenciaAsync();
        MostrarEstado(Estado.Exito, rfc: rfc);
    }

    private void MostrarEstado(Estado estado, string? mensajeError = null, string? rfc = null)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
            SuccessIcon.IsVisible = ErrorIcon.IsVisible = false;
            RfcPanel.IsVisible = ContinuarBtn.IsVisible = false;

            switch (estado)
            {
                case Estado.Cargando:
                    LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
                    TituloLabel.Text  = "Activando créditos...";
                    MensajeLabel.Text = "Por favor espera un momento.";
                    break;

                case Estado.Exito:
                    SuccessIcon.IsVisible  = true;
                    TituloLabel.Text       = "¡Créditos activados!";
                    MensajeLabel.Text      = "Tus 15 créditos ya están disponibles.";
                    RfcLabel.Text          = rfc ?? string.Empty;
                    RfcPanel.IsVisible     = true;
                    ContinuarBtn.IsVisible = true;
                    break;

                case Estado.Error:
                    ErrorIcon.IsVisible    = true;
                    TituloLabel.Text       = "No se pudo activar";
                    MensajeLabel.Text      = mensajeError ?? "Algo salió mal.";
                    ContinuarBtn.IsVisible = true;
                    break;
            }
        });
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private enum Estado { Cargando, Exito, Error }
}
