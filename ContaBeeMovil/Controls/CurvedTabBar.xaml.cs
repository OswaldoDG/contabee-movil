namespace ContaBeeMovil.Controls;

public partial class CurvedTabBar : ContentView
{
    public event EventHandler<int>? TabChanged;

    private const float RestingNotchDepth = 45f;
    private const float FlatNotchDepth    = 5f;   // iguala TopOffset del drawable → barra plana
    private readonly bool _initialized;

    // ── BarColor — sincroniza drawable + botón flotante ───────────────────
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

    // ── SelectedIndex ─────────────────────────────────────────────────────
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
        CurvedBackground.Drawable = _drawable;
        UpdateVisualState(0);
        _initialized = true;
    }

    // ── Posición normalizada de la curva por índice ───────────────────────
    private static float GetNotchPosition(int index) => index switch
    {
        0 => 0.25f,
        1 => 0.75f,
        _ => 0.5f
    };

    // ── Tap handlers ──────────────────────────────────────────────────────
    private void OnTabInicio_Tapped(object? sender, TappedEventArgs e)      => SelectTab(0);
    private void OnTabFacturacion_Tapped(object? sender, TappedEventArgs e) => SelectTab(1);
    private void OnFloatingButton_Tapped(object? sender, TappedEventArgs e) { /* tab ya activo */ }

    private void SelectTab(int index)
    {
        if (SelectedIndex == index) return;
        SelectedIndex = index;
        TabChanged?.Invoke(this, index);
    }

    // ── Estado visual ─────────────────────────────────────────────────────
    private void UpdateVisualState(int index)
    {
        // Ícono del botón flotante
        FloatingIconHome.IsVisible        = index == 0;
        FloatingIconFacturacion.IsVisible = index == 1;

        // El botón flotante muestra icono + label del tab activo
        FloatingLabel.Text = index == 0 ? "Inicio" : "Facturación";

        // Ocultar el tab interno completo cuando está activo
        TabInicio.Opacity           = index == 0 ? 0 : 1;
        TabInicio.InputTransparent  = index == 0;
        TabFacturacion.Opacity      = index == 1 ? 0 : 1;
        TabFacturacion.InputTransparent = index == 1;

        // El botón salta inmediatamente, solo la muesca anima
        PositionFloatingButton(index, Width);
        AnimateNotch(index);
    }

    // ── Animación alternativa: deslizamiento horizontal ───────────────────
    // Para volver a ella, reemplaza la llamada AnimateNotch(index) en
    // UpdateVisualState por AnimateNotchSlide(index).
    //private void AnimateNotchSlide(int newIndex)
    //{
    //    float fromPosition = _drawable.NotchPosition;
    //    float toPosition   = GetNotchPosition(newIndex);
    //    if (!_initialized) { _drawable.NotchPosition = toPosition; CurvedBackground.Invalidate(); return; }
    //    this.AbortAnimation("NotchAnim");
    //    var anim = new Animation(v =>
    //    {
    //        _drawable.NotchPosition = (float)v;
    //        CurvedBackground.Invalidate();
    //    }, fromPosition, toPosition);
    //    anim.Commit(this, "NotchAnim", length: 250, easing: Easing.CubicInOut);
    //}

    // ── Animación en dos fases: retrae → reaparece ────────────────────────
    private void AnimateNotch(int newIndex)
    {
        float toPosition = GetNotchPosition(newIndex);

        // Sin animación en la carga inicial
        if (!_initialized)
        {
            _drawable.NotchPosition = toPosition;
            CurvedBackground.Invalidate();
            PositionFloatingButton(newIndex, Width);
            return;
        }

        this.AbortAnimation("NotchAnim");

        // Fase 1 — retrae la muesca actual (depth → plano)
        var retract = new Animation(v =>
        {
            _drawable.NotchDepth = (float)v;
            CurvedBackground.Invalidate();
        }, _drawable.NotchDepth, FlatNotchDepth, Easing.CubicIn);

        retract.Commit(this, "NotchAnim", length: 150, finished: (_, cancelled) =>
        {
            if (cancelled) return;

            // Mueve la muesca al nuevo icono cuando está plana (el botón ya está ahí)
            _drawable.NotchPosition = toPosition;

            // Fase 2 — reaparece la muesca en el nuevo icono (depth → completo)
            var expand = new Animation(v =>
            {
                _drawable.NotchDepth = (float)v;
                CurvedBackground.Invalidate();
            }, FlatNotchDepth, RestingNotchDepth, Easing.CubicOut);

            expand.Commit(this, "NotchAnim", length: 150);
        });
    }

    // ── Posicionamiento horizontal del botón ──────────────────────────────
    private void PositionFloatingButton(int index, double totalWidth)
    {
        if (totalWidth <= 0)
        {
            Dispatcher.Dispatch(() => PositionFloatingButton(index, Width));
            return;
        }
        FloatingButton.TranslationX = totalWidth * GetNotchPosition(index) - totalWidth / 2.0;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0)
        {
            PositionFloatingButton(SelectedIndex, width);
            CurvedBackground.Invalidate();
        }
    }
}
