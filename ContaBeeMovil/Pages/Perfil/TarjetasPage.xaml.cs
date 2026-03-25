using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using ContaBeeMovil.Helpers;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Perfil;

public partial class TarjetasPage : ContentPage
{
    private readonly IServicioSesion _sesion;
    private readonly ObservableCollection<TarjetaModel> _tarjetas = new();

    // Evita re-entradas mientras hay un popup activo
    private bool _enCRUD = false;

    private double ScreenWidth => DeviceDisplay.MainDisplayInfo.Width
                                  / DeviceDisplay.MainDisplayInfo.Density;
    private const double CardRatio = 1.9;

    // Overlay oscuro que replica la apariencia original del modal
    private static readonly PopupOptions _popupOpts = new()
    {
        PageOverlayColor = Color.FromArgb("#66000000"),
        CanBeDismissedByTappingOutsideOfPopup = false,
    };

    public TarjetasPage(IServicioSesion sesion)
    {
        InitializeComponent();
        _sesion = sesion;
        ListaTarjetas.ItemsSource = _tarjetas;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var bg = AppBackground();
        BackgroundColor        = bg;
        RootGrid.BackgroundColor = bg;

        if (_enCRUD) return;
        CargarTarjetas();
    }

    // ── Helpers de color ──────────────────────────────────────────────────────

    // Evita devolver Transparent si UIHelpers no encuentra la clave
    private static Color AppBackground()
    {
        var c = UIHelpers.GetColor("Background");
        if (c == Colors.Transparent)
            c = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#141414")
                : Color.FromArgb("#fefdfc");
        return c;
    }

    // ── Carga desde AppState ──────────────────────────────────────────────────

    private void CargarTarjetas()
    {
        _tarjetas.Clear();
        foreach (var t in AppState.Instance.Tarjetas ?? Enumerable.Empty<TarjetaModel>())
            _tarjetas.Add(t);

        ActualizarVisibilidad();
    }

    // ── Muestra lista o placeholder según haya tarjetas ───────────────────────

    private void ActualizarVisibilidad()
    {
        var hayTarjetas = _tarjetas.Count > 0;

        if (hayTarjetas)
        {
            // Fijar el fondo antes de hacer visible el CollectionView
            // para que Android nunca muestre el gris del RecyclerView.
            ListaTarjetas.BackgroundColor = AppBackground();
            ListaTarjetas.IsVisible = true;
            EmptyLayout.IsVisible   = false;
        }
        else
        {
            ListaTarjetas.IsVisible = false;
            EmptyLayout.IsVisible   = true;
        }
    }

    // ── Guarda en SecureStorage y AppState ───────────────────────────────────

    private async Task SincronizarAsync()
        => await _sesion.GuardarTarjetasAsync(_tarjetas.ToList());

    // ── Gradiente + altura responsiva ─────────────────────────────────────────

    private void OnCardLoaded(object sender, EventArgs e)
    {
        if (sender is not Border border) return;

        var cardWidth  = ScreenWidth - 32;
        var cardHeight = cardWidth / CardRatio;
        if (border.Parent is SwipeView swipeView)
        {
            swipeView.HeightRequest   = cardHeight;
            swipeView.BackgroundColor = AppBackground();
        }

        border.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(1, 1),
            GradientStops =
            [
                new GradientStop(UIHelpers.GetColor("Primary"),  0f),
                new GradientStop(UIHelpers.GetColor("Tertiary"), 1f),
            ]
        };
    }

    // ── Eliminar ─────────────────────────────────────────────────────────────

    private async void OnEliminarTarjeta(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        if (swipeItem.BindingContext is not TarjetaModel tarjeta) return;

        bool confirmar = await DisplayAlert(
            "Eliminar tarjeta",
            $"¿Deseas eliminar la tarjeta \"{tarjeta.Alias}\"?",
            "Eliminar",
            "Cancelar");

        if (confirmar)
        {
            _tarjetas.Remove(tarjeta);
            ActualizarVisibilidad();
            LoadingOverlay.IsVisible = true;
            try   { await SincronizarAsync(); }
            finally { LoadingOverlay.IsVisible = false; }
        }
    }

    // ── Agregar ───────────────────────────────────────────────────────────────

    private async void OnAgregarTarjeta(object sender, EventArgs e)
    {
        if (_enCRUD) return;
        _enCRUD = true;
        try
        {
            TarjetaModel? nueva = null;
            await this.ShowPopupAsync(
                new TarjetaFormPopup(result => nueva = result),
                _popupOpts,
                CancellationToken.None);

            if (nueva is not null)
            {
                _tarjetas.Add(nueva);
                ActualizarVisibilidad();
                await SincronizarAsync();
            }
        }
        finally
        {
            _enCRUD = false;
        }
    }

    // ── Editar ────────────────────────────────────────────────────────────────

    private async void OnEditarTarjeta(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TarjetaModel tarjeta) return;
        if (_enCRUD) return;
        _enCRUD = true;
        try
        {
            TarjetaModel? actualizada = null;
            await this.ShowPopupAsync(
                new TarjetaFormPopup(result => actualizada = result, tarjeta),
                _popupOpts,
                CancellationToken.None);

            if (actualizada is not null)
            {
                var existente = _tarjetas.FirstOrDefault(t => t.Id == actualizada.Id);
                if (existente != null)
                {
                    var index = _tarjetas.IndexOf(existente);
                    _tarjetas.RemoveAt(index);
                    _tarjetas.Insert(index, actualizada);
                }
                await SincronizarAsync();
                CargarTarjetas();
            }
        }
        finally
        {
            _enCRUD = false;
        }
    }
}
