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

    public SimpleTabBar()
    {
        InitializeComponent();
        UpdateVisualState(0);
    }

    private void OnTabInicio_Tapped(object? sender, TappedEventArgs e)      => SelectTab(0);
    private void OnTabFacturacion_Tapped(object? sender, TappedEventArgs e) => SelectTab(1);
    private void OnTabEquipo_Tapped(object? sender, TappedEventArgs e)      => SelectTab(2);

    private void SelectTab(int index)
    {
        if (SelectedIndex == index) return;
        SelectedIndex = index;
        TabChanged?.Invoke(this, index);
    }

    private void UpdateVisualState(int index)
    {
        UnderlineInicio.IsVisible      = index == 0;
        UnderlineFacturacion.IsVisible = index == 1;
        UnderlineEquipo.IsVisible      = index == 2;
    }
}
