using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Views;

public partial class ActividadView : ContentView
{
    private const string PrefModoCreditos = "Home_ModoCreditos";
    // 0 = Captura + Colab + Auto  |  1 = Captura + Colab  |  2 = Solo Captura
    private int _modoCreditos;

    // Labels cuyo binding quedó roto por la animación (Text seteado directo), pendientes de
    // restaurar tras GetLicenciaAsync para evitar el salto visual al final.
    private readonly List<(Label numero, string bindingPath)> _bindingsPendientes = new();

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
        // Desactivado. Para reactivar: descomentar el GestureRecognizer en ActividadView.xaml
        // y descomentar las siguientes 3 líneas:
        //_modoCreditos = (_modoCreditos + 1) % 3;
        //Preferences.Default.Set(PrefModoCreditos, _modoCreditos);
        //AplicarModoCreditos();
    }

    public void ResaltarCreditos(params CreditoGanado[] creditos)
        => _ = ResaltarCreditosAsync(creditos)
            .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(RestaurarBindingsCreditos));

    /// <summary>
    /// Muestra el valor viejo, sube el "+N" y cuenta hasta el nuevo valor para cada crédito ganado.
    /// Deja el label mostrando el valor final con el binding roto; el caller DEBE llamar
    /// GetLicenciaAsync y luego <see cref="RestaurarBindingsCreditos"/> para evitar el salto visual.
    /// Solo anima las tarjetas visibles según el modo de créditos actual.
    /// </summary>
    public Task ResaltarCreditosAsync(params CreditoGanado[] creditos)
    {
        _bindingsPendientes.Clear();
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
            // MasNConConteo rompe el binding (setea Text directo) y deja el label en `hasta`.
            // Se restaura recién en RestaurarBindingsCreditos, tras sincronizar el licenciamiento.
            _bindingsPendientes.Add((numero, bindingPath));
            tareas.Add(AnimacionesCredito.MasNConConteo(border, badge, numero, desde, hasta, UIHelpers.GetColor(colorKey)));
        }
        return tareas.Count > 0 ? Task.WhenAll(tareas) : Task.CompletedTask;
    }

    /// <summary>
    /// Restaura el data binding de los labels animados. Debe llamarse DESPUÉS de GetLicenciaAsync
    /// para que el binding se evalúe contra el valor real del servidor (= valor final de la
    /// animación) y el label no salte al valor viejo y de vuelta.
    /// </summary>
    public void RestaurarBindingsCreditos()
    {
        foreach (var (numero, bindingPath) in _bindingsPendientes)
            numero.SetBinding(Label.TextProperty, new Binding(bindingPath));
        _bindingsPendientes.Clear();
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
