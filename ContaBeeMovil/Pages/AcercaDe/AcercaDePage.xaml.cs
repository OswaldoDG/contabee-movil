using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using Contabee.Api.abstractions;
using MauiIcons.Core;
using MauiIcons.Material;

namespace ContaBeeMovil.Pages.AcercaDe;

public partial class AcercaDePage : ContentPage
{
    private readonly IServicioSalud _servicioSalud;
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioSesion _servicioSesion;
    private CancellationTokenSource? _ctsLoader;
    private CancellationTokenSource? _ctsLoaderCargaActual;

    private static bool UsuarioLogueado
        => Application.Current?.Windows.FirstOrDefault()?.Page is Shell;

    public AcercaDePage(IServicioSalud servicioSalud, IServicioTranscript servicioTranscript,IServicioSesion servicioSesion)
    {
        InitializeComponent();
        _servicioSalud = servicioSalud;
        _servicioTranscript = servicioTranscript;
        this._servicioSesion = servicioSesion;
        LabelVersion.Text = $"Versión {AppInfo.VersionString}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

    private async void OnAtrasTapped(object? sender, TappedEventArgs e)
    {
        if (UsuarioLogueado && Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
        else
            await Navigation.PopAsync();
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

    private void AplicarIconoEstado(MauiIcon icono, bool ok)
    {
        icono.Icon(ok ? MaterialIcons.CheckCircle : MaterialIcons.Cancel);
        icono.IconColor = ok
            ? UIHelpers.GetColor("Success")
            : UIHelpers.GetColor("Error");
    }
}
