namespace ContaBeeMovil.Views;

/// <summary>
/// Aviso animado estilo tutorial de videojuego: la mascota se deja caer sobre el borde
/// superior del cuadro de diálogo y respira mientras el aviso está en pantalla.
/// </summary>
/// <remarks>
/// No es un popup: se monta como una capa más de la página (normalmente anclada al
/// fondo, encima de la barra de botones) y <b>no bloquea</b> — no hay velo y el resto de
/// la pantalla sigue respondiendo. Eso es deliberado en el caso del horario de captura:
/// el aviso informa, pero el usuario puede seguir enviando.
/// <para>
/// <b>No se retrae solo.</b> Se queda hasta que lo cierren con la ✕ o tocando la caja.
/// Hubo una versión que se iba a los 6 s y se descartó: dejaba al usuario sin forma de
/// releer el mensaje, porque la franja de estado que servía para volver a llamarlo está
/// desactivada.
/// </para>
/// <para>
/// El bamboleo es una <see cref="Animation"/> comprometida contra esta vista y no un
/// bucle de <c>Task.Delay</c>: va sincronizada al ticker de la plataforma (se ve fluida)
/// y se corta de golpe con <c>AbortAnimation</c>, que es lo que necesita al ser un bucle
/// infinito.
/// </para>
/// </remarks>
public partial class MascotaAvisoView : ContentView
{
    private const uint MsEntradaCaja  = 420;
    private const uint MsCaidaMascota = 520;
    private const uint MsSalida       = 260;

    /// <summary>Bamboleo en bucle; se aborta por nombre.</summary>
    private const string AnimBamboleo = "BamboleoMascota";

    public MascotaAvisoView()
    {
        InitializeComponent();
    }

    // ── API ──────────────────────────────────────────────────────────────────

    public static readonly BindableProperty MensajeProperty = BindableProperty.Create(
        nameof(Mensaje), typeof(string), typeof(MascotaAvisoView), string.Empty,
        propertyChanged: (bindable, _, valor) =>
            ((MascotaAvisoView)bindable).LblMensaje.Text = valor as string ?? string.Empty);

    /// <summary>Texto del cuadro de diálogo.</summary>
    public string Mensaje
    {
        get => (string)GetValue(MensajeProperty);
        set => SetValue(MensajeProperty, value);
    }

    public static readonly BindableProperty TituloProperty = BindableProperty.Create(
        nameof(Titulo), typeof(string), typeof(MascotaAvisoView), "Fuera de horario",
        propertyChanged: (bindable, _, valor) =>
            ((MascotaAvisoView)bindable).LblTitulo.Text = valor as string ?? string.Empty);

    /// <summary>Encabezado en negrita, junto al reloj.</summary>
    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    /// <summary>
    /// Representa el aviso. Se queda en pantalla hasta que lo cierren; llamarlo de nuevo
    /// reinicia la escena.
    /// </summary>
    public async Task MostrarAsync()
    {
        DetenerBamboleo();

        // Fuera de pantalla por la izquierda. Se usa el ancho del display y no el de la
        // caja porque con IsVisible=false la vista todavía no está medida (Width = 0);
        // pasarse de largo no se nota, quedarse corto sí (la caja "aparecería" a medio
        // camino en vez de entrar desde el borde).
        var desplazamiento = -AnchoPantalla();

        Caja.TranslationX    = desplazamiento;
        Caja.Opacity         = 0;
        Mascota.TranslationY = -70;
        Mascota.Scale        = 0.5;
        Mascota.Rotation     = -12;
        Mascota.Opacity      = 0;
        Contenedor.Opacity   = 1;
        IsVisible            = true;

        // 1) La caja entra deslizándose y, sin esperar a que termine, la mascota se
        //    deja caer desde arriba. Dos vectores distintos a la vez es lo que da la
        //    sensación de escena montada; que todo entre junto es lo que hace que se
        //    lea como una notificación.
        var entradaCaja = Task.WhenAll(
            Caja.TranslateTo(0, 0, MsEntradaCaja, Easing.CubicOut),
            Caja.FadeTo(1, 200, Easing.CubicOut));

        await Task.Delay(130);

        // BounceOut en la caída y SpringOut en escala y giro: la mascota aterriza,
        // rebota y se endereza, en vez de simplemente aparecer.
        var entradaMascota = Task.WhenAll(
            Mascota.FadeTo(1, 150),
            Mascota.TranslateTo(0, 0, MsCaidaMascota, Easing.BounceOut),
            Mascota.ScaleTo(1, 400, Easing.SpringOut),
            Mascota.RotateTo(0, 420, Easing.SpringOut));

        await Task.WhenAll(entradaCaja, entradaMascota);

        // 2) Ya en su sitio, respira. El bucle corre hasta que se cierre el aviso: es
        //    lo que hace que el personaje se sienta vivo y no una imagen pegada.
        IniciarBamboleo();
    }

    /// <summary>Retrae el aviso por donde entró.</summary>
    public async Task OcultarAsync()
    {
        if (!IsVisible) return;

        DetenerBamboleo();

        // La mascota se va primero, encogiéndose hacia arriba, y la caja la sigue: el
        // orden inverso al de entrada cierra la escena en vez de cortarla.
        _ = Task.WhenAll(
            Mascota.ScaleTo(0.6, 200, Easing.CubicIn),
            Mascota.TranslateTo(0, -40, 220, Easing.CubicIn),
            Mascota.FadeTo(0, 180));

        await Task.Delay(90);

        await Task.WhenAll(
            Caja.TranslateTo(-AnchoPantalla(), 0, MsSalida, Easing.CubicIn),
            Caja.FadeTo(0, 220, Easing.CubicIn));

        IsVisible = false;
    }

    /// <summary>
    /// Lo quita sin animación. Para <c>OnDisappearing</c>: si se dejara corriendo el
    /// bamboleo —que es un bucle infinito— seguiría animando sobre una página que ya no
    /// está.
    /// </summary>
    public void OcultarInmediato()
    {
        DetenerBamboleo();

        foreach (var vista in new VisualElement[] { Contenedor, Caja, Mascota })
        {
            vista.AbortAnimation("TranslateTo");
            vista.AbortAnimation("FadeTo");
            vista.AbortAnimation("ScaleTo");
            vista.AbortAnimation("RotateTo");
        }

        IsVisible          = false;
        Contenedor.Opacity = 0;
    }

    // ── Bamboleo ─────────────────────────────────────────────────────────────

    /// <summary>Sube y baja a la mascota unos pocos pixeles, en bucle: "respira".</summary>
    private void IniciarBamboleo()
    {
        var bamboleo = new Animation();
        bamboleo.Add(0.0, 0.5, new Animation(v => Mascota.TranslationY = v,  0, -6, Easing.SinInOut));
        bamboleo.Add(0.5, 1.0, new Animation(v => Mascota.TranslationY = v, -6,  0, Easing.SinInOut));

        bamboleo.Commit(this, AnimBamboleo, 16, 1700, repeat: () => true);
    }

    private void DetenerBamboleo()
    {
        this.AbortAnimation(AnimBamboleo);
        Mascota.TranslationY = 0;
    }

    // ── Interno ──────────────────────────────────────────────────────────────

    private async void OnCerrarTapped(object sender, TappedEventArgs e) => await OcultarAsync();

    private async void OnTapped(object sender, TappedEventArgs e) => await OcultarAsync();

    private static double AnchoPantalla()
    {
        var info  = DeviceDisplay.Current.MainDisplayInfo;
        var ancho = info.Density > 0 ? info.Width / info.Density : info.Width;

        // Antes del primer layout el display puede reportar 0; 420 dips cubre de sobra
        // cualquier teléfono y sólo afecta a la distancia que recorre la animación.
        return ancho > 0 ? ancho : 420;
    }
}
