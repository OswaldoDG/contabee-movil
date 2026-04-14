namespace ContaBeeMovil.Controls;

public partial class CurvedTabBar : ContentView
{
    public event EventHandler<int>? TabChanged;

    private float _restingNotchDepth;
    private readonly bool _initialized;

    public static readonly BindableProperty BarColorProperty =
        BindableProperty.Create(
            nameof(BarColor),
            typeof(Color),
            typeof(CurvedTabBar),
            defaultValue: Color.FromArgb("#F5A623"),
            propertyChanged: (b, _, n) =>
            {
                if (b is CurvedTabBar tb && n is Color c)
                {
                    tb._drawable.BarColor = c;
                    tb.CurvedBackground.Invalidate();
                }
            });

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(CurvedTabBar),
            defaultValue: 0,
            propertyChanged: (b, _, n) => ((CurvedTabBar)b).UpdateVisualState((int)n));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private readonly CurvedTabBarDrawable _drawable = new();

    public CurvedTabBar()
    {
        InitializeComponent();
        SetAdaptiveHeight();
        CurvedBackground.Drawable = _drawable;
        UpdateVisualState(0);
        _initialized = true;
    }

    // ── Altura y posiciones base ──────────────────────────────────────────
    private void SetAdaptiveHeight()
    {
        double screenHeight = DeviceDisplay.MainDisplayInfo.Height
                            / DeviceDisplay.MainDisplayInfo.Density;

        double barHeight = DeviceInfo.Idiom == DeviceIdiom.Tablet
            ? 75
            : Math.Clamp(screenHeight * 0.08, 60, 80);

        this.HeightRequest = barHeight;

        // Sin padding: los tabs se centran solos en el bar completo
        TabsGrid.Padding = new Thickness(0);

        _restingNotchDepth = (float)(barHeight * 0.72);
        _drawable.NotchDepth = _restingNotchDepth;
        _drawable.NotchDepthRatio = 0.72f;

        // Centrar el botón flotante dentro de la "gota":
        // el bulbo de la gota está aprox. entre 0 y notchDepth → centro ≈ notchDepth*0.55
        double floatingHeight = FloatingButton.HeightRequest;
        FloatingButton.TranslationY = (_restingNotchDepth * 0.55) - (floatingHeight / 2.0);
    }

    private static float GetNotchPosition(int index) => index switch
    {
        0 => 0.25f,
        1 => 0.75f,
        _ => 0.5f
    };

    // ── Tap handlers ──────────────────────────────────────────────────────
    private void OnTabInicio_Tapped(object? sender, TappedEventArgs e) => SelectTab(0);
    private void OnTabFacturacion_Tapped(object? sender, TappedEventArgs e) => SelectTab(1);
    private void OnFloatingButton_Tapped(object? sender, TappedEventArgs e) { }

    private void SelectTab(int index)
    {
        if (SelectedIndex == index) return;
        SelectedIndex = index;
        TabChanged?.Invoke(this, index);
    }

    // ── Estado visual ─────────────────────────────────────────────────────
    private void UpdateVisualState(int index)
    {
        // Tabs: el seleccionado se oculta (lo cubre el botón flotante)
        TabInicio.Opacity = index == 0 ? 0 : 1;
        TabInicio.InputTransparent = index == 0;
        TabFacturacion.Opacity = index == 1 ? 0 : 1;
        TabFacturacion.InputTransparent = index == 1;

        // Icono correcto en el botón flotante
        FloatingIconHome.IsVisible = index == 0;
        FloatingIconFacturacion.IsVisible = index == 1;
        FloatingIconHome.Opacity = 1;
        FloatingIconFacturacion.Opacity = 1;

        if (!_initialized)
        {
            _drawable.NotchPosition = GetNotchPosition(index);
            _drawable.NotchDepth = _restingNotchDepth;
            CurvedBackground.Invalidate();
            PositionFloatingButtonInstant(index, Width);
            return;
        }

        DropNotch(index);
    }

    // ── Slide horizontal: la gota se desplaza al nuevo lado ───────────────
    private void DropNotch(int newIndex)
    {
        double totalWidth = Width;
        if (totalWidth <= 0)
        {
            Dispatcher.Dispatch(() => DropNotch(newIndex));
            return;
        }

        this.AbortAnimation("NotchAnim");

        float fromPosition = _drawable.NotchPosition;
        float toPosition = GetNotchPosition(newIndex);

        if (Math.Abs(fromPosition - toPosition) < 0.001f)
            return;

        // Mantiene la profundidad estable; solo desliza la posición
        _drawable.NotchDepth = _restingNotchDepth;

        new Animation(v =>
        {
            float pos = (float)v;
            _drawable.NotchPosition = pos;
            FloatingButton.TranslationX = totalWidth * pos - totalWidth / 2.0;
            CurvedBackground.Invalidate();
        }, fromPosition, toPosition, Easing.CubicInOut)
        .Commit(
            this,
            "NotchAnim",
            rate: 16,
            length: 240,
            finished: (_, __) =>
            {
                _drawable.NotchPosition = toPosition;
                _drawable.NotchDepth = _restingNotchDepth;
                FloatingButton.TranslationX = totalWidth * toPosition - totalWidth / 2.0;
                CurvedBackground.Invalidate();
            });
    }

    private void PositionFloatingButtonInstant(int index, double totalWidth)
    {
        if (totalWidth <= 0)
        {
            Dispatcher.Dispatch(() => PositionFloatingButtonInstant(index, Width));
            return;
        }
        FloatingButton.TranslationX = totalWidth * GetNotchPosition(index) - totalWidth / 2.0;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
            PositionFloatingButtonInstant(SelectedIndex, width);
    }
}