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

    public void ResaltarCreditos(params CreditoGanado[] creditos)
        => _ = ResaltarCreditosAsync(creditos);

    /// <summary>
    /// Muestra el valor viejo, sube el "+N" y cuenta hasta el nuevo valor para cada crédito ganado.
    /// Restaura el data binding del label al terminar; llama GetLicenciaAsync después de awaitar.
    /// Solo anima las tarjetas visibles según el modo de créditos actual.
    /// </summary>
    public Task ResaltarCreditosAsync(params CreditoGanado[] creditos)
    {
        var tareas = new List<Task>();
        foreach (var c in creditos)
        {
            var (border, badge, numero, colorKey, bindingPath) = c.Tipo switch
            {
                TipoCreditoResaltar.Captura      => (BorderCaptura, BadgeCaptura, LabelCaptura, "Captura",    "CreditosCapturaDisponibles"),
                TipoCreditoResaltar.Autoservicio => (BorderAuto,    BadgeAuto,    LabelAuto,    "Auto",       "CreditosAutoDisponibles"),
                _                                => (BorderColab,   BadgeColab,   LabelColab,   "Colab",      "CreditosColabDisponibles"),
            };

            if (!border.IsVisible) continue;

            // El label aún muestra el valor viejo (GetLicenciaAsync no se ha llamado).
            int desde = int.TryParse(numero.Text, out var v) ? v : 0;
            int hasta = desde + c.Cantidad;
            tareas.Add(AnimarConBinding(border, badge, numero, desde, hasta, UIHelpers.GetColor(colorKey), bindingPath));
        }
        return tareas.Count > 0 ? Task.WhenAll(tareas) : Task.CompletedTask;
    }

    private static async Task AnimarConBinding(Border border, Label badge, Label numero, int desde, int hasta, Color color, string bindingPath)
    {
        await AnimacionesCredito.MasNConConteo(border, badge, numero, desde, hasta, color);
        // ContarLabel rompe el binding (setea Text directamente); lo restauramos aquí.
        numero.SetBinding(Label.TextProperty, new Binding(bindingPath));
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
