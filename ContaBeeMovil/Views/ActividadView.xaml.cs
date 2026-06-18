using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Views;

public partial class ActividadView : ContentView
{
    private const string PrefModoCreditos = "Home_ModoCreditos";
    // 0 = Captura + Colab + Auto  |  1 = Captura + Colab  |  2 = Solo Captura
    private int _modoCreditos;

    public ActividadView()
    {
        InitializeComponent();

        _modoCreditos = Preferences.Default.Get(PrefModoCreditos, 0);
        AplicarModoCreditos();

        PullRefresh.HandlerChanged += (_, _) => AplicarColorRefresh();

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (_, _) =>
                MainThread.BeginInvokeOnMainThread(AplicarColorRefresh);
        }
    }

    private void OnCreditosTapped(object sender, TappedEventArgs e)
    {
        _modoCreditos = (_modoCreditos + 1) % 3;
        Preferences.Default.Set(PrefModoCreditos, _modoCreditos);
        AplicarModoCreditos();
    }

    /// <summary>
    /// Resalta con un pulso de escala + glow de color las tarjetas de los tipos
    /// de crédito indicados (solo las visibles según el modo de créditos actual).
    /// </summary>
    public void ResaltarCreditos(params TipoCreditoResaltar[] tipos)
    {
        foreach (var tipo in tipos.Distinct())
        {
            var (border, colorKey) = tipo switch
            {
                TipoCreditoResaltar.Captura      => (BorderCaptura, "Captura"),
                TipoCreditoResaltar.Autoservicio => (BorderAuto,    "Auto"),
                _                                => (BorderColab,   "Colab"),
            };

            if (!border.IsVisible) continue;   // tarjeta oculta por el modo de créditos
            _ = AnimarTarjeta(border, UIHelpers.GetColor(colorKey));
        }
    }

    private static async Task AnimarTarjeta(Border border, Color color)
    {
        var strokeOriginal = border.Stroke;
        var grosorOriginal = border.StrokeThickness;

        border.Stroke = new SolidColorBrush(color);   // glow: el borde toma el color del tipo

        await Task.WhenAll(
            border.ScaleToAsync(1.12, 180, Easing.CubicOut),
            AnimarGrosorBorde(border, grosorOriginal, 3, 180));

        await Task.WhenAll(
            border.ScaleToAsync(1.0, 200, Easing.CubicIn),
            AnimarGrosorBorde(border, 3, grosorOriginal, 200));

        border.Stroke = strokeOriginal;               // restaura el borde Accent1
    }

    private static Task AnimarGrosorBorde(Border border, double desde, double hasta, uint duracion)
    {
        var tcs = new TaskCompletionSource();
        var anim = new Animation(v => border.StrokeThickness = v, desde, hasta, Easing.CubicInOut);
        anim.Commit(border, "AnimGrosorBorde", length: duracion, finished: (_, _) => tcs.SetResult());
        return tcs.Task;
    }

    private void AplicarModoCreditos()
    {
        var mostrarColab = _modoCreditos < 2;
        var mostrarAuto  = _modoCreditos == 0;

        VStackColab.IsVisible = mostrarColab;
        VStackAuto.IsVisible  = mostrarAuto;
        ColColab.Width = mostrarColab ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ColAuto.Width  = mostrarAuto  ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void AplicarColorRefresh()
    {
#if ANDROID
        if (PullRefresh.Handler?.PlatformView is AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout swipe)
        {
            var primary = UIHelpers.GetColor("Primary");
            var yellow = new Android.Graphics.Color(
                (byte)(primary.Red * 255),
                (byte)(primary.Green * 255),
                (byte)(primary.Blue * 255));
            swipe.SetProgressBackgroundColorSchemeColor(yellow);
            swipe.SetColorSchemeColors(Android.Graphics.Color.Black);
        }
#elif IOS
        if (PullRefresh.Handler?.PlatformView is UIKit.UIView platformView)
        {
            MakeScrollViewTransparent(platformView);
            var uiRefresh = FindUIRefreshControl(platformView);
            if (uiRefresh != null)
            {
                uiRefresh.BackgroundColor = UIKit.UIColor.Clear;
                uiRefresh.TintColor = UIKit.UIColor.Black;
            }
        }
#endif
    }

#if IOS
    private static UIKit.UIRefreshControl? FindUIRefreshControl(UIKit.UIView view)
    {
        if (view is UIKit.UIScrollView scrollView)
            return scrollView.RefreshControl;

        foreach (var subview in view.Subviews)
        {
            var result = FindUIRefreshControl(subview);
            if (result != null) return result;
        }
        return null;
    }

    private static void MakeScrollViewTransparent(UIKit.UIView view)
    {
        if (view is UIKit.UIScrollView scrollView)
            scrollView.BackgroundColor = UIKit.UIColor.Clear;

        foreach (var subview in view.Subviews)
            MakeScrollViewTransparent(subview);
    }
#endif
}
