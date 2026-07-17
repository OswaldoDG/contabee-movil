using Contabee.Api.abstractions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.Dev;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.AcercaDe;

public partial class AcercaDePage : ContentPage
{
    private readonly IServicioSalud _servicioSalud;
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServicioToast _servicioToast;
    private CancellationTokenSource? _ctsLoader;
    private CancellationTokenSource? _ctsLoaderCargaActual;
    private int _tapCount = 0;
    private const string ClaveMododDev = "ModoDeveloper";

    private static bool UsuarioLogueado
        => Application.Current?.Windows.FirstOrDefault()?.Page is Shell;

    public AcercaDePage(IServicioSalud servicioSalud, IServicioTranscript servicioTranscript, IServicioSesion servicioSesion, IServicioAlmacenamiento almacenamiento, IServicioToast servicioToast)
    {
        InitializeComponent();
        _servicioSalud = servicioSalud;
        _servicioTranscript = servicioTranscript;
        _servicioSesion = servicioSesion;
        _almacenamiento = almacenamiento;
        _servicioToast = servicioToast;
        LabelVersion.Text = $"Versión {AppInfo.VersionString}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _tapCount = 0;
        ActualizarInternet();
        Connectivity.ConnectivityChanged += OnConectividadCambiada;
        await VerificarServiciosAsync();
        await CargarInstantaneosAsync();
        _ = CargarDatosSesionAsync();
    }

    private async Task CargarDatosSesionAsync()
    {
        var idDispositivo = await _servicioSesion.LeeIdDeDispositivo();

        LblIdDispositivo.Text = string.IsNullOrWhiteSpace(idDispositivo) ? "No disponible" : idDispositivo;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.ConnectivityChanged -= OnConectividadCambiada;
        _ctsLoader?.Cancel();
        _ctsLoaderCargaActual?.Cancel();
    }

    private async void OnVersionTapped(object? sender, TappedEventArgs e)
    {
        if (AppState.Instance.EsDev)
        {
            _tapCount++;
            if (_tapCount % 2 == 0)
                await _servicioToast.MostrarAsync("Modo Desarrollador ya activo", ToastIcono.Info, ToastPosicion.Bottom);
            return;
        }

        _tapCount++;

        if (_tapCount >= 10)
        {
            var dto = new ModoDeveloperDto
            {
                EsDev = true,
                FechaActivacion = DateTime.UtcNow.ToString("O")
            };
            await _almacenamiento.GuardarSeguroAsync(ClaveMododDev, dto);
            AppState.Instance.EsDev = true;
            await _servicioToast.MostrarAsync("Modo Desarrollador activado", ToastIcono.Info, ToastPosicion.Bottom, duracionMs: 1000);
            _tapCount = 0;
        }
    }

    private async void OnAtrasTapped(object? sender, TappedEventArgs e)
    {
        if (UsuarioLogueado && Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
        else
            await Navigation.PopAsync();
    }

    private async void OnLogsTapped(object? sender, TappedEventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<LogsPage>();
        await Navigation.PushAsync(page);
    }

    private void OnConectividadCambiada(object? sender, ConnectivityChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(ActualizarInternet);

    private void ActualizarInternet()
    {
        bool conectado = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        AplicarIconoEstado(IconoInternet, conectado);
    }

    private async Task VerificarServiciosAsync()
    {
        LoaderServicios.IsVisible = true;
        IconoServicios.IsVisible = false;

        _ctsLoader = new CancellationTokenSource();
        var animacion = AnimarPuntosAsync(_ctsLoader.Token);

        bool ok = await _servicioSalud.VerificarServiciosAsync();

        _ctsLoader.Cancel();
        await animacion;

        LoaderServicios.IsVisible = false;
        IconoServicios.IsVisible = true;
        AplicarIconoEstado(IconoServicios, ok);
    }

    private async Task CargarInstantaneosAsync()
    {
        LoaderCargaActual.IsVisible = true;
        LabelCargaActual.IsVisible = false;

        _ctsLoaderCargaActual = new CancellationTokenSource();
        var animacion = AnimarPuntosCargaActualAsync(_ctsLoaderCargaActual.Token);

        var respuesta = await _servicioTranscript.ObtenerInstantaneosAsync(_ctsLoaderCargaActual.Token);

        _ctsLoaderCargaActual.Cancel();
        await animacion;

        LoaderCargaActual.IsVisible = false;
        LabelCargaActual.IsVisible = true;

        if (respuesta.Ok && respuesta.Payload is not null)
        {
            LabelCargaActual.Text = $"{respuesta.Payload.Pendientes}";
            LabelCargaActual.TextColor = UIHelpers.GetColor("Error");
            LabelCargaActual.FontSize = 17;
        }
        else
        {
            LabelCargaActual.Text = "--";
            LabelCargaActual.TextColor = UIHelpers.GetColor("SecondaryText");
            LabelCargaActual.FontSize = 17;
        }
    }

    private async Task AnimarPuntosAsync(CancellationToken token)
    {
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            LoaderServicios.Text = new string('.', (i % 3) + 1);
            i++;
            try { await Task.Delay(400, token); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task AnimarPuntosCargaActualAsync(CancellationToken token)
    {
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            LoaderCargaActual.Text = new string('.', (i % 3) + 1);
            i++;
            try { await Task.Delay(400, token); }
            catch (TaskCanceledException) { break; }
        }
    }

    private void AplicarIconoEstado(Label icono, bool ok)
    {
        icono.Text = ok ? FluentUIFilled.checkmark_circle_20_filled : FluentUIFilled.dismiss_circle_20_filled;
        icono.TextColor = ok
            ? UIHelpers.GetColor("Success")
            : UIHelpers.GetColor("Error");
    }
}
