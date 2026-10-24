namespace ContaBeeMovil.Views;

/// <summary>
/// Esqueleto de carga para listas: tarjetas grises que pulsan mientras <see cref="Activo"/> es
/// true. Se percibe más corto que un spinner y, cuando los datos salen de caché, confirma que
/// el toque sí hizo algo.
/// </summary>
public partial class EsqueletoListaView : ContentView
{
    private const string AnimacionPulso = "PulsoEsqueleto";

    public static readonly BindableProperty ActivoProperty =
        BindableProperty.Create(nameof(Activo), typeof(bool), typeof(EsqueletoListaView), false,
            propertyChanged: (b, _, n) => ((EsqueletoListaView)b).CambiarActivo((bool)n));

    public bool Activo
    {
        get => (bool)GetValue(ActivoProperty);
        set => SetValue(ActivoProperty, value);
    }

    public EsqueletoListaView()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(Esqueleto, Enumerable.Range(0, 6).ToList());
    }

    private void CambiarActivo(bool activo)
    {
        this.AbortAnimation(AnimacionPulso);
        Esqueleto.Opacity = 1;
        if (!activo) return;

        new Animation
        {
            { 0, 0.5, new Animation(v => Esqueleto.Opacity = v, 1, 0.45, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => Esqueleto.Opacity = v, 0.45, 1, Easing.SinInOut) },
        }.Commit(this, AnimacionPulso, length: 1400, repeat: () => Activo);
    }
}
