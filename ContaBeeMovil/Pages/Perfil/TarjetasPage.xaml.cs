using System.Collections.ObjectModel;
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

    // Evita que OnAppearing pise cambios mientras hay un popup activo
    private bool _enCRUD = false;

    private double ScreenWidth => DeviceDisplay.MainDisplayInfo.Width
                                  / DeviceDisplay.MainDisplayInfo.Density;
    private const double CardRatio = 1.9;

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
        // Si hay un CRUD en curso no recargamos: evitamos que el popup
        // dispare OnAppearing y pise la colección con datos viejos.
        if (_enCRUD) return;
        CargarTarjetas();
    }

    // ── Carga desde AppState (ya fue poblado en PosLoginAsync) ───────────────

    private void CargarTarjetas()
    {
        _tarjetas.Clear();
        foreach (var t in AppState.Instance.Tarjetas ?? Enumerable.Empty<TarjetaModel>())
            _tarjetas.Add(t);
    }

    // ── Guarda colección actual en SecureStorage y AppState ───────────────────

    private async Task SincronizarAsync()
        => await _sesion.GuardarTarjetasAsync(_tarjetas.ToList());

    // ── Gradiente + altura responsiva (un solo evento Loaded) ────────────────

    private void OnCardLoaded(object sender, EventArgs e)
    {
        if (sender is not Border border) return;

        // Altura en el padre SwipeView para evitar que aparezca aplastado
        var cardWidth  = ScreenWidth - 32;
        var cardHeight = cardWidth / CardRatio;
        if (border.Parent is SwipeView swipeView)
            swipeView.HeightRequest = cardHeight;

        // Gradiente con colores del tema
        var inicio = UIHelpers.GetColor("Primary");
        var fin    = UIHelpers.GetColor("Tertiary");

        border.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(1, 1),
            GradientStops =
            [
                new GradientStop(inicio, 0f),
                new GradientStop(fin,    1f),
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
            await SincronizarAsync();
        }
    }

    // ── Agregar ───────────────────────────────────────────────────────────────

    private async void OnAgregarTarjeta(object sender, EventArgs e)
    {
        _enCRUD = true;
        try
        {
            var popup = new TarjetaFormPopup();
            await this.ShowPopupAsync(popup);

            if (popup.Resultado is TarjetaModel nueva)
            {
                _tarjetas.Add(nueva);
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

        _enCRUD = true;
        try
        {
            var popup = new TarjetaFormPopup(tarjeta);
            await this.ShowPopupAsync(popup);

            if (popup.Resultado is TarjetaModel actualizada)
            {
                var vieja = _tarjetas.FirstOrDefault(t => t.Id == actualizada.Id);
                var index = vieja is not null ? _tarjetas.IndexOf(vieja) : -1;

                if (index >= 0)
                {
                    // Remove + Insert dispara Remove+Add en ObservableCollection,
                    // que CollectionView maneja de forma confiable en Android
                    // (Replace a veces no refresca la celda)
                    _tarjetas.RemoveAt(index);
                    _tarjetas.Insert(index, actualizada);
                }

                await SincronizarAsync();
            }
        }
        finally
        {
            _enCRUD = false;
        }
    }
}
