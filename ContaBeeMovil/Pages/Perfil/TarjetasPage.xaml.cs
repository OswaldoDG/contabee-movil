using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
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

    // Tracks the previous TotalX per item Grid to compute deltas between gesture events
    private readonly Dictionary<Grid, double> _prevTotalX = new();

    // Ancho de pantalla en dp (calculado una vez)
    private double ScreenWidth => DeviceDisplay.MainDisplayInfo.Width
                                  / DeviceDisplay.MainDisplayInfo.Density;

    // Ratio ancho:alto de la tarjeta
    private const double CardRatio = 1.9;

    public TarjetasPage(IServicioSesion sesion)
    {
        InitializeComponent();
        _sesion = sesion;
        ListaTarjetas.ItemsSource = _tarjetas;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarTarjetasAsync();
    }

    // ── Carga desde AppState ──────────────────────────────────────────────────

    private async Task CargarTarjetasAsync()
    {
        await _sesion.GetTarjetasAsync();

        _tarjetas.Clear();
        foreach (var t in AppState.Instance.Tarjetas ?? Enumerable.Empty<TarjetaModel>())
            _tarjetas.Add(t);
    }

    private async Task GuardarTarjetasAsync()
        => await _sesion.GuardarTarjetasAsync(_tarjetas.ToList());

    // ── Tamaño responsivo del Grid contenedor ────────────────────────────────

    private void OnItemGridLoaded(object sender, EventArgs e)
    {
        if (sender is not Grid grid) return;

        var cardWidth  = ScreenWidth - 32;
        var cardHeight = cardWidth / CardRatio;
        grid.HeightRequest = cardHeight;
    }

    // ── Gradiente de la tarjeta via UIHelpers + Colors.xaml ──────────────────

    private void OnCardLoaded(object sender, EventArgs e)
    {
        if (sender is not Border border) return;
        AplicarGradiente(border);
    }

    private static void AplicarGradiente(Border border)
    {
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

    // ── Pan gesture: deslizar la tarjeta a la derecha ─────────────────────────
    //
    // El PanGestureRecognizer está en el Grid externo (hijo directo del RecyclerView)
    // para evitar que Android intercepte el gesto antes de que llegue aquí.
    // Solo trasladamos el Border de la tarjeta (Children[1]), no el Grid completo,
    // para que el fondo rojo quede visible debajo.
    //
    // Usamos delta tracking porque en Android el gesture puede reiniciarse
    // (TotalX vuelve a 0) sin pasar por GestureStatus.Started.

    private void OnCardPan(object sender, PanUpdatedEventArgs e)
    {
        if (sender is not Grid itemGrid) return;
        if (itemGrid.Children.Count < 2 || itemGrid.Children[1] is not Border card) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _prevTotalX[itemGrid] = 0;
                break;

            case GestureStatus.Running:
            {
                // GetValueOrDefault con e.TotalX como fallback: si Started no llegó (común en Android
                // dentro de CollectionView), el primer delta es 0 y los siguientes son correctos.
                var prev = _prevTotalX.GetValueOrDefault(itemGrid, e.TotalX);
                var delta = e.TotalX - prev;
                _prevTotalX[itemGrid] = e.TotalX;

                // Solo hacia la derecha
                card.TranslationX = Math.Max(0, card.TranslationX + delta);
                break;
            }

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _prevTotalX.Remove(itemGrid);
                _ = HandlePanEndAsync(card, itemGrid.BindingContext as TarjetaModel);
                break;
        }
    }

    private async Task HandlePanEndAsync(Border card, TarjetaModel? tarjeta)
    {
        var umbral = ScreenWidth * 0.55;   // 55% del ancho dispara eliminación

        if (card.TranslationX >= umbral)
        {
            bool confirmar = await DisplayAlert(
                "Eliminar tarjeta",
                $"¿Deseas eliminar la tarjeta \"{tarjeta?.Alias}\"?",
                "Eliminar",
                "Cancelar");

            if (confirmar && tarjeta is not null)
            {
                _tarjetas.Remove(tarjeta);
                await GuardarTarjetasAsync();
            }
            else
            {
                // Regresa suavemente
                await card.TranslateTo(0, 0, 420, Easing.SpringOut);
            }
        }
        else
        {
            // No llegó al umbral → regresa con spring
            await card.TranslateTo(0, 0, 400, Easing.SpringOut);
        }
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    private async void OnAgregarTarjeta(object sender, EventArgs e)
    {
        var popup = new TarjetaFormPopup();
        await this.ShowPopupAsync(popup);

        if (popup.Resultado is TarjetaModel nueva)
        {
            _tarjetas.Add(nueva);
            await GuardarTarjetasAsync();
        }
    }

    private async void OnEditarTarjeta(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TarjetaModel tarjeta) return;

        var popup = new TarjetaFormPopup(tarjeta);
        await this.ShowPopupAsync(popup);

        if (popup.Resultado is TarjetaModel actualizada)
        {
            var index = _tarjetas.IndexOf(
                _tarjetas.FirstOrDefault(t => t.Id == actualizada.Id)!);

            if (index >= 0)
                _tarjetas[index] = actualizada;

            await GuardarTarjetasAsync();
        }
    }
}
