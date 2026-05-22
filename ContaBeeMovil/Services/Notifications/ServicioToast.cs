using ContaBeeMovil.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace ContaBeeMovil.Services.Notifications;

public enum ToastIcono { Info, Warning, Error }
public enum ToastPosicion { Top, Center, Bottom }

public interface IServicioToast
{
    Task MostrarAsync(
        string mensaje,
        ToastIcono icono = ToastIcono.Info,
        ToastPosicion posicion = ToastPosicion.Bottom,
        int duracionMs = 3000,
        bool reemplazarAnteriores = false);
}

public class ServicioToast : IServicioToast
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _reemplazoLock = new();
    private CancellationTokenSource? _ctsReemplazo;
    private Grid? _toastReemplazoActivo;
    private Grid? _overlayReemplazoActivo;

    public async Task MostrarAsync(
        string mensaje,
        ToastIcono icono = ToastIcono.Info,
        ToastPosicion posicion = ToastPosicion.Bottom,
        int duracionMs = 3000,
        bool reemplazarAnteriores = false)
    {
        if (reemplazarAnteriores)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var pagina = ObtenerPaginaActual();
                if (pagina == null) return;

                CancellationTokenSource cts;
                CancellationToken token;
                CancellationTokenSource? ctsAnterior;
                lock (_reemplazoLock)
                {
                    ctsAnterior = _ctsReemplazo;
                    _ctsReemplazo?.Cancel();
                    _ctsReemplazo = new CancellationTokenSource();
                    cts = _ctsReemplazo;
                    token = cts.Token;

                    if (_overlayReemplazoActivo != null && _toastReemplazoActivo != null && _overlayReemplazoActivo.Children.Contains(_toastReemplazoActivo))
                        _overlayReemplazoActivo.Children.Remove(_toastReemplazoActivo);

                    _toastReemplazoActivo = null;
                    _overlayReemplazoActivo = null;
                }

                ctsAnterior?.Dispose();

                var capaOverlay = ObtenerOCrearOverlay(pagina);
                var toast = ConstruirVista(mensaje, icono, posicion, cts);

                double entradaY = posicion switch
                {
                    ToastPosicion.Top => -80,
                    ToastPosicion.Bottom => 80,
                    _ => 0
                };

                toast.Opacity = 0;
                toast.TranslationY = entradaY;
                toast.InputTransparent = false;
                capaOverlay.Children.Add(toast);

                lock (_reemplazoLock)
                {
                    _toastReemplazoActivo = toast;
                    _overlayReemplazoActivo = capaOverlay;
                }

                await Task.WhenAll(
                    toast.FadeToAsync(1, 120, Easing.CubicOut),
                    toast.TranslateToAsync(0, 0, 120, Easing.CubicOut)
                );

                try { await Task.Delay(Math.Max(duracionMs, 150), token); }
                catch (TaskCanceledException) { }

                if (token.IsCancellationRequested)
                    return;

                await Task.WhenAll(
                    toast.FadeToAsync(0, 100, Easing.CubicIn),
                    toast.TranslateToAsync(0, entradaY, 100, Easing.CubicIn)
                );

                if (capaOverlay.Children.Contains(toast))
                    capaOverlay.Children.Remove(toast);

                lock (_reemplazoLock)
                {
                    if (ReferenceEquals(_toastReemplazoActivo, toast))
                    {
                        _toastReemplazoActivo = null;
                        _overlayReemplazoActivo = null;
                        if (ReferenceEquals(_ctsReemplazo, cts))
                            _ctsReemplazo = null;
                    }
                }

                cts.Dispose();
            });

            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var pagina = ObtenerPaginaActual();
                if (pagina == null) return;

                var cts = new CancellationTokenSource();
                var capaOverlay = ObtenerOCrearOverlay(pagina);
                var toast = ConstruirVista(mensaje, icono, posicion, cts);

                double entradaY = posicion switch
                {
                    ToastPosicion.Top => -80,
                    ToastPosicion.Bottom => 80,
                    _ => 0
                };

                toast.Opacity = 0;
                toast.TranslationY = entradaY;
                toast.InputTransparent = false;
                capaOverlay.Children.Add(toast);

                await Task.WhenAll(
                    toast.FadeToAsync(1, 280, Easing.CubicOut),
                    toast.TranslateToAsync(0, 0, 280, Easing.CubicOut)
                );

                try { await Task.Delay(Math.Max(duracionMs, 150), cts.Token); }
                catch (TaskCanceledException) { }

                await Task.WhenAll(
                    toast.FadeToAsync(0, 220, Easing.CubicIn),
                    toast.TranslateToAsync(0, entradaY, 220, Easing.CubicIn)
                );

                if (capaOverlay.Children.Contains(toast))
                    capaOverlay.Children.Remove(toast);
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static ContentPage? ObtenerPaginaActual()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Page == null) return null;

        var pagina = window.Page;

        if (pagina.Navigation?.ModalStack?.Count > 0)
            pagina = pagina.Navigation.ModalStack.Last();

        return pagina switch
        {
            NavigationPage nav => nav.CurrentPage as ContentPage,
            Shell shell => shell.CurrentPage as ContentPage,
            ContentPage cp => cp,
            _ => null
        };
    }

    private static Grid ObtenerOCrearOverlay(ContentPage pagina)
    {
        // Si el root ya es un Grid que contiene nuestra capa, reutilizarla
        if (pagina.Content is Grid gridRoot)
        {
            var existente = gridRoot.Children
                .OfType<Grid>()
                .FirstOrDefault(c => c.ClassId == "ToastOverlay_Layer");
            if (existente != null) return existente;

            // Agregar capa directamente al Grid existente sin re-parentizar su contenido
            var capa = new Grid
            {
                ClassId = "ToastOverlay_Layer",
                BackgroundColor = Colors.Transparent,
                InputTransparent = true,
                CascadeInputTransparent = false,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            int filas = Math.Max(gridRoot.RowDefinitions.Count, 1);
            int cols  = Math.Max(gridRoot.ColumnDefinitions.Count, 1);
            Grid.SetRow(capa, 0);
            Grid.SetColumn(capa, 0);
            Grid.SetRowSpan(capa, filas);
            Grid.SetColumnSpan(capa, cols);
            gridRoot.Children.Add(capa);
            return capa;
        }

        // Root no es Grid: envolver una sola vez y nunca restaurar
        var contenidoOriginal = pagina.Content;
        var raiz = new Grid { ClassId = "ToastOverlay_Root" };
        raiz.Children.Add(contenidoOriginal);

        var capaFallback = new Grid
        {
            ClassId = "ToastOverlay_Layer",
            BackgroundColor = Colors.Transparent,
            InputTransparent = true,
            CascadeInputTransparent = false,
        };
        raiz.Children.Add(capaFallback);
        pagina.Content = raiz;
        return capaFallback;
    }

    private static Grid ConstruirVista(string mensaje, ToastIcono icono, ToastPosicion posicion, CancellationTokenSource cts)
    {
        var color = UIHelpers.GetColor("Primary");

        var (verticalOptions, margin) = posicion switch
        {
            ToastPosicion.Top    => (LayoutOptions.Start,  new Thickness(20, 50, 5, 0)),
            ToastPosicion.Center => (LayoutOptions.Center, new Thickness(20, 0, 5, 0)),
            ToastPosicion.Bottom => (LayoutOptions.End,    new Thickness(20, 5, 20, 5)),
            _                    => (LayoutOptions.Start,  new Thickness(20, 50, 10, 0))
        };

        var frame = new Border
        {
            BackgroundColor = Color.FromArgb("00FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(5, 5, 5, 150) },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 5,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
        };

        var colorIcono = UIHelpers.GetColor(icono switch
        {
            ToastIcono.Error   => "Error",
            ToastIcono.Warning => "Warning",
            _                  => "Success",
        });
        var textoIcono = icono switch
        {
            ToastIcono.Info    => "i",
            ToastIcono.Warning => "!",
            ToastIcono.Error   => "✕",
            _                  => "i"
        };

        var iconFrame = new Border
        {
            BackgroundColor = colorIcono,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Colors.Transparent,
            WidthRequest = 28,
            HeightRequest = 28,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = textoIcono,
                TextColor = Colors.White,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            }
        };

        var label = new Label
        {
            Text = mensaje,
            TextColor = UIHelpers.GetColor("PrimaryText"),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
        };

        var closeLabel = new Label
        {
            Text = "✕",
            TextColor = Color.FromArgb("#999999"),
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
        };

        var accent = new BoxView
        {
            Color = color,
            CornerRadius = new CornerRadius(5, 5, 5, 5),
            BackgroundColor = Color.FromArgb("00FFFFFF"),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Offset = new Point(0, 8),
                Radius = 20f,
                Opacity = 0.35f,
            },
        };

        var accent2 = new BoxView
        {
            Color = UIHelpers.GetColor("Background"),
            CornerRadius = new CornerRadius(5, 5, 5, 150),
            BackgroundColor = Color.FromArgb("00FFFFFF"),
        };

        var stack = new Grid { HorizontalOptions = LayoutOptions.Fill };

        var wrapper = new Grid
        {
            Margin = margin,
            VerticalOptions = verticalOptions,
            HorizontalOptions = LayoutOptions.Fill,
        };

        grid.Add(iconFrame, 0, 0);
        grid.Add(label, 1, 0);
        grid.Add(closeLabel, 2, 0);

        frame.Content = grid;

        stack.Children.Add(accent2);
        stack.Children.Add(frame);
        stack.Padding = new Thickness(3, 3);

        wrapper.Children.Add(accent);
        wrapper.Children.Add(stack);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => cts.Cancel();
        wrapper.GestureRecognizers.Add(tap);

        return wrapper;
    }
}
