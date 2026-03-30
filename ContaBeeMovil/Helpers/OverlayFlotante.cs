using Microsoft.Maui.Controls.Shapes;

namespace ContaBeeMovil.Helpers;

internal static class OverlayFlotante
{
    private const string OverlayTag = "OverlayFlotante";
    private static Grid? _overlayActual;
    private static Layout? _raizActual;
    private static EventHandler? _handlerDesapareciendo;

    internal static async Task MostrarEnPagina(View trigger, View contenido, double anchoMinimo = 0)
    {
        Ocultar();

        var raiz = trigger.ObtenerLayoutRaizPagina();
        if (raiz is null) return;

        var pagina = trigger.ObtenerPagina();

        var posicion = ObtenerPosicionRelativa(trigger, raiz);
        var altoPagina = raiz.Height > 0 ? raiz.Height : DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;

        var ancho = anchoMinimo > 0 ? anchoMinimo : trigger.Width;
        if (ancho <= 0) ancho = 200;

        var contenedorDropdown = new Border
        {
            BackgroundColor = UIHelpers.GetColor("Background"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = new SolidColorBrush(UIHelpers.GetColor("Accent1")),
            StrokeThickness = 1,
            Content = contenido,
            WidthRequest = ancho,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(UIHelpers.GetColor("Shadow")),
                Offset = new Point(0, 4),
                Radius = 12,
                Opacity = 0.15f
            },
            Opacity = 0,
            TranslationY = -8,
        };

        double top = posicion.Y + trigger.Height + 4;
        double left = posicion.X;

        bool desbordaAbajo = top + 300 > altoPagina;
        if (desbordaAbajo)
            top = posicion.Y - 4;

        contenedorDropdown.Margin = new Thickness(left, top, 0, 0);

        if (desbordaAbajo)
        {
            contenedorDropdown.VerticalOptions = LayoutOptions.Start;
            contenedorDropdown.AnchorY = 1;
        }

        var overlay = new Grid
        {
            AutomationId = OverlayTag,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = false,
            CascadeInputTransparent = false,
            ZIndex = 9999,
            Children = { contenedorDropdown }
        };

        var tapDismiss = new TapGestureRecognizer();
        tapDismiss.Tapped += (_, _) => Ocultar();
        overlay.GestureRecognizers.Add(tapDismiss);

        if (raiz is Grid grid)
        {
            Grid.SetRowSpan(overlay, Math.Max(grid.RowDefinitions.Count, 1));
            Grid.SetColumnSpan(overlay, Math.Max(grid.ColumnDefinitions.Count, 1));
        }

        _overlayActual = overlay;
        _raizActual = raiz;

        raiz.Children.Add(overlay);

        if (pagina is not null)
        {
            _handlerDesapareciendo = (_, _) => Ocultar();
            pagina.Disappearing += _handlerDesapareciendo;
        }

        await Task.WhenAll(
            contenedorDropdown.FadeToAsync(1, 150, Easing.CubicOut),
            contenedorDropdown.TranslateToAsync(0, 0, 150, Easing.CubicOut)
        );
    }

    internal static void Ocultar()
    {
        if (_overlayActual is null || _raizActual is null) return;

        var overlay = _overlayActual;
        var raiz = _raizActual;

        _overlayActual = null;
        _raizActual = null;

        if (_handlerDesapareciendo is not null)
        {
            var pagina = raiz.ObtenerPagina();
            if (pagina is not null)
                pagina.Disappearing -= _handlerDesapareciendo;
            _handlerDesapareciendo = null;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            raiz.Children.Remove(overlay);
        });
    }

    internal static bool EstaVisible => _overlayActual is not null;

    private static Point ObtenerPosicionRelativa(View elemento, Layout raiz)
    {
        double x = 0, y = 0;
        VisualElement? actual = elemento;

        while (actual is not null && actual != raiz)
        {
            x += actual.X;
            y += actual.Y;
            actual = actual.Parent as VisualElement;
        }

        return new Point(x, y);
    }
}
