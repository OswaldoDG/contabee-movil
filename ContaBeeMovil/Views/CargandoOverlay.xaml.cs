namespace ContaBeeMovil.Views;

public partial class CargandoOverlay : ContentView
{
    public static readonly BindableProperty TextoProperty =
        BindableProperty.Create(nameof(Texto), typeof(string), typeof(CargandoOverlay), string.Empty,
            propertyChanged: (b, _, n) => ((CargandoOverlay)b).ActualizarTexto((string)n));

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public CargandoOverlay() => InitializeComponent();

    private void ActualizarTexto(string texto)
    {
        if (LblTexto is null) return;
        LblTexto.Text = texto;
        LblTexto.IsVisible = !string.IsNullOrEmpty(texto);
    }
}
