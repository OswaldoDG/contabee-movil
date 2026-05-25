using ContaBeeMovil.Helpers;
using ContaBeeMovil.Services;
using MauiIcons.Core;
using MauiIcons.Material;

namespace ContaBeeMovil.Pages.AcercaDe;

public partial class AcercaDePage : ContentPage
{
    private readonly IServicioSalud _servicioSalud;
    private CancellationTokenSource? _ctsLoader;

    private static bool UsuarioLogueado
        => Application.Current?.Windows.FirstOrDefault()?.Page is Shell;

    public AcercaDePage(IServicioSalud servicioSalud)
    {
        InitializeComponent();
        _servicioSalud = servicioSalud;
        LabelVersion.Text = $"Versión {AppInfo.VersionString}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ActualizarInternet();
        Connectivity.ConnectivityChanged += OnConectividadCambiada;
        await VerificarServiciosAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.ConnectivityChanged -= OnConectividadCambiada;
        _ctsLoader?.Cancel();
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

    private void AplicarIconoEstado(MauiIcon icono, bool ok)
    {
        icono.Icon(ok ? MaterialIcons.CheckCircle : MaterialIcons.Cancel);
        icono.IconColor = ok
            ? UIHelpers.GetColor("Success")
            : UIHelpers.GetColor("Error");
    }
}
