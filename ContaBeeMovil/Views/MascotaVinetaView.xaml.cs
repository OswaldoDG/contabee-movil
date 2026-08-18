namespace ContaBeeMovil.Views;

/// <summary>
/// Aviso en forma de tira de cómic vertical pegada al borde izquierdo: la mascota al pie
/// y, justo sobre su cabeza, una pill con el título y un globo de diálogo con el mensaje.
/// Entra deslizándose desde la izquierda y las dos piezas brotan una tras otra.
/// </summary>
/// <remarks>
/// Alternativa a <see cref="MascotaAvisoView"/> (cuadro de diálogo horizontal con la
/// mascota parada sobre el borde superior). Las dos exponen la misma API —
/// <c>MostrarAsync</c> / <c>OcultarAsync</c> / <c>OcultarInmediato</c> y las propiedades
/// <c>Titulo</c> y <c>Mensaje</c>— así que cambiar de una a otra es cambiar el tipo en
/// el XAML de <c>PaginaCaptura</c>, sin tocar el code-behind de la página.
/// <para>
/// No bloquea, no lleva velo y <b>no se retrae solo</b>: se queda hasta que lo cierren
/// con la ✕ o tocando la viñeta.
/// </para>
/// </remarks>
public partial class MascotaVinetaView : ContentView
{
    private const uint MsEntradaVineta = 440;
    private const uint MsSalida        = 260;

    /// <summary>Bamboleo en bucle; se aborta por nombre.</summary>
    private const string AnimBamboleo = "BamboleoMascota";

    /// <summary>
    /// Fracción del ancho del PNG, medida desde su borde izquierdo, donde TERMINA el
    /// dibujo. El archivo es cuadrado y trae aire transparente a los lados: el dibujo va
    /// del 25.75 % al 70 %. Medido a ojo sobre el PNG; si se cambia el arte, se remide.
    /// </summary>
    private const double FraccionDerechaDibujo = 0.70;

    public MascotaVinetaView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Cuántos pixeles ocupa realmente la tira contando desde el borde izquierdo de la
    /// pantalla — o sea, dónde termina la mascota y empieza el hueco libre.
    /// </summary>
    /// <remarks>
    /// Existe para que la página pueda acomodar el selector "Quién captura" en ese hueco
    /// sin repetir aquí las medidas de la mascota. Se calcula a partir de lo que dice el
    /// XAML (el margen negativo y el ancho de la imagen) y no de constantes propias, para
    /// que cambiar el tamaño de la mascota no exija acordarse de tocar este número.
    /// <para>
    /// Es el ancho del DIBUJO, no el de la caja de la imagen: la caja se pasa de largo
    /// hacia la derecha con puro aire transparente, y dejarle ese aire al selector sería
    /// regalarle ~85 px de nada.
    /// </para>
    /// </remarks>
    public double AnchoOcupado =>
        Math.Max(0, Mascota.Margin.Left + FraccionDerechaDibujo * Mascota.WidthRequest);

    // ── API ──────────────────────────────────────────────────────────────────

    public static readonly BindableProperty MensajeProperty = BindableProperty.Create(
        nameof(Mensaje), typeof(string), typeof(MascotaVinetaView), string.Empty,
        propertyChanged: (bindable, _, valor) =>
            ((MascotaVinetaView)bindable).LblMensaje.Text = valor as string ?? string.Empty);

    /// <summary>Texto del segundo globo.</summary>
    public string Mensaje
    {
        get => (string)GetValue(MensajeProperty);
        set => SetValue(MensajeProperty, value);
    }

    public static readonly BindableProperty TituloProperty = BindableProperty.Create(
        nameof(Titulo), typeof(string), typeof(MascotaVinetaView), "Fuera de horario",
        propertyChanged: (bindable, _, valor) =>
            ((MascotaVinetaView)bindable).LblTitulo.Text = valor as string ?? string.Empty);

    /// <summary>Texto del primer globo.</summary>
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
        // viñeta porque con IsVisible=false la vista todavía no está medida (Width = 0);
        // pasarse de largo no se nota, quedarse corto sí — la tira "aparecería" a medio
        // camino en vez de entrar desde el borde.
        var desplazamiento = -AnchoPantalla();

        Vineta.TranslationX  = desplazamiento;
        Vineta.Opacity       = 0;
        Mascota.TranslationX = desplazamiento;
        Mascota.TranslationY = 0;
        Mascota.Opacity      = 0;
        Mascota.Scale        = 0.55;
        Mascota.Rotation     = -10;
        Globo1.Scale         = 0;
        Globo2.Scale         = 0;
        Pico2.Opacity        = 0;
        Contenedor.Opacity   = 1;
        IsVisible            = true;

        // 1) Entra la tira completa desde el borde. La mascota se anima aparte porque
        //    ya no vive dentro del Border de los globos —se salió para poder ser más
        //    grande que la media pantalla—, pero con los mismos tiempos: sigue leyéndose
        //    como una sola pieza que entra.
        await Task.WhenAll(
            Vineta.TranslateTo(0, 0, MsEntradaVineta, Easing.CubicOut),
            Vineta.FadeTo(1, 200, Easing.CubicOut),
            Mascota.TranslateTo(0, 0, MsEntradaVineta, Easing.CubicOut),
            Mascota.FadeTo(1, 200, Easing.CubicOut));

        // 2) La mascota se planta, girando hasta enderezarse.
        await Task.WhenAll(
            Mascota.ScaleTo(1, 380, Easing.SpringOut),
            Mascota.RotateTo(0, 400, Easing.SpringOut));

        // 3) Y recién entonces habla: primero brota la pill del título y después el
        //    globo del mensaje, UNO TRAS OTRO y no a la vez. Ese escalonado es lo que se
        //    lee como diálogo; simultáneos parecerían dos bloques de texto que
        //    aparecieron solos.
        //    El pico se prende de golpe junto con su globo: animarlo aparte deja ver un
        //    triángulo suelto flotando bajo un globo que todavía no existe.
        await Globo1.ScaleTo(1, 300, Easing.SpringOut);

        await Task.Delay(120);

        Pico2.Opacity = 1;
        await Globo2.ScaleTo(1, 300, Easing.SpringOut);

        // 4) Ya montada la escena, la mascota respira. El bucle corre hasta que se
        //    cierre el aviso: es lo que hace que el personaje se sienta vivo y no una
        //    imagen pegada.
        IniciarBamboleo();
    }

    /// <summary>Retrae la tira por donde entró.</summary>
    public async Task OcultarAsync()
    {
        if (!IsVisible) return;

        DetenerBamboleo();

        var desplazamiento = -AnchoPantalla();

        await Task.WhenAll(
            Vineta.TranslateTo(desplazamiento, 0, MsSalida, Easing.CubicIn),
            Vineta.FadeTo(0, 220, Easing.CubicIn),
            Mascota.TranslateTo(desplazamiento, 0, MsSalida, Easing.CubicIn),
            Mascota.FadeTo(0, 220, Easing.CubicIn));

        IsVisible = false;
    }

    /// <summary>
    /// La quita sin animación. Para <c>OnDisappearing</c>: si se dejara corriendo el
    /// bamboleo —que es un bucle infinito— seguiría animando sobre una página que ya no
    /// está.
    /// </summary>
    public void OcultarInmediato()
    {
        DetenerBamboleo();

        foreach (var vista in new VisualElement[] { Contenedor, Vineta, Mascota, Globo1, Globo2 })
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
        bamboleo.Add(0.0, 0.5, new Animation(v => Mascota.TranslationY = v,  0, -5, Easing.SinInOut));
        bamboleo.Add(0.5, 1.0, new Animation(v => Mascota.TranslationY = v, -5,  0, Easing.SinInOut));

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
