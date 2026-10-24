using ContaBeeMovil.Helpers;
namespace ContaBeeMovil.Controls;

public partial class SimpleTabBar : ContentView
{
    public event EventHandler<int>? TabChanged;

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SimpleTabBar),
            defaultValue: 0,
            propertyChanged: (b, _, n) => ((SimpleTabBar)b).UpdateVisualState((int)n));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public bool ResaltarSoloTabsPrincipales { get; set; } = false;

    public SimpleTabBar()
    {
        InitializeComponent();
        UpdateVisualState(0);
    }

    private void OnTabInicio_Tapped(object? sender, TappedEventArgs e)        => SelectTab(0);
    private void OnTabFacturacion_Tapped(object? sender, TappedEventArgs e)   => SelectTab(1);
    private void OnTabDevoluciones_Tapped(object? sender, TappedEventArgs e)  => SelectTab(2);
    private void OnTabComprobaciones_Tapped(object? sender, TappedEventArgs e) => SelectTab(3);
    private void OnTabEquipo_Tapped(object? sender, TappedEventArgs e)        => SelectTab(4);

    private void SelectTab(int index)
    {
        if (SelectedIndex == index) return;
        SelectedIndex = index;
        TabChanged?.Invoke(this, index);
    }

    /// <summary>Muestra u oculta una pestaña; oculta, su columna mide 0 y las demás se reparten el ancho.</summary>
    public void SetTabVisible(int index, bool visible)
    {
        View[] tabs = [TabInicio, TabFacturacion, TabDevoluciones, TabComprobaciones, TabEquipo];
        tabs[index].IsVisible = visible;
        TabsGrid.ColumnDefinitions[index].Width = visible ? GridLength.Star : new GridLength(0);
    }

    private void UpdateVisualState(int index)
    {
        UnderlineInicio.IsVisible         = index == 0;
        UnderlineFacturacion.IsVisible    = index == 1;
        UnderlineDevoluciones.IsVisible   = index == 2;
        UnderlineComprobaciones.IsVisible = index == 3;
        UnderlineEquipo.IsVisible         = index == 4;
    }
}
