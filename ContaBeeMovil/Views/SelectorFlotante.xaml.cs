using System.Collections;
using System.Windows.Input;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Views;

public partial class SelectorFlotante : ContentView
{
    public static readonly BindableProperty TituloProperty =
        BindableProperty.Create(nameof(Titulo), typeof(string), typeof(SelectorFlotante),
            defaultValue: string.Empty, propertyChanged: OnAparienciaChanged);

    public static readonly BindableProperty ElementosProperty =
        BindableProperty.Create(nameof(Elementos), typeof(IList), typeof(SelectorFlotante),
            defaultValue: null, propertyChanged: OnAparienciaChanged);

    public static readonly BindableProperty IndiceSeleccionadoProperty =
        BindableProperty.Create(nameof(IndiceSeleccionado), typeof(int), typeof(SelectorFlotante),
            defaultValue: -1, defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnSeleccionChanged);

    public static readonly BindableProperty ElementoSeleccionadoProperty =
        BindableProperty.Create(nameof(ElementoSeleccionado), typeof(object), typeof(SelectorFlotante),
            defaultValue: null, defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnElementoSeleccionadoChanged);

    public static readonly BindableProperty SeleccionCambiadaCommandProperty =
        BindableProperty.Create(nameof(SeleccionCambiadaCommand), typeof(ICommand), typeof(SelectorFlotante));

    public static readonly BindableProperty MaxAltoListaProperty =
        BindableProperty.Create(nameof(MaxAltoLista), typeof(double), typeof(SelectorFlotante), defaultValue: 300.0);

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public IList? Elementos
    {
        get => (IList?)GetValue(ElementosProperty);
        set => SetValue(ElementosProperty, value);
    }

    public int IndiceSeleccionado
    {
        get => (int)GetValue(IndiceSeleccionadoProperty);
        set => SetValue(IndiceSeleccionadoProperty, value);
    }

    public object? ElementoSeleccionado
    {
        get => GetValue(ElementoSeleccionadoProperty);
        set => SetValue(ElementoSeleccionadoProperty, value);
    }

    public ICommand? SeleccionCambiadaCommand
    {
        get => (ICommand?)GetValue(SeleccionCambiadaCommandProperty);
        set => SetValue(SeleccionCambiadaCommandProperty, value);
    }

    public double MaxAltoLista
    {
        get => (double)GetValue(MaxAltoListaProperty);
        set => SetValue(MaxAltoListaProperty, value);
    }

    public event EventHandler<int>? IndiceCambiado;

    private bool _sincronizando;

    public SelectorFlotante()
    {
        InitializeComponent();
        ActualizarTexto();
    }

    private void OnTriggerTapped(object? sender, TappedEventArgs e)
    {
        if (OverlayFlotante.EstaVisible)
        {
            OverlayFlotante.Ocultar();
            return;
        }
        _ = MostrarDropdown();
    }

    private async Task MostrarDropdown()
    {
        var elementos = Elementos;
        if (elementos is null || elementos.Count == 0) return;

        var lista = new VerticalStackLayout { Spacing = 0 };

        for (int i = 0; i < elementos.Count; i++)
        {
            var indice = i;
            var texto = elementos[i]?.ToString() ?? string.Empty;
            bool seleccionado = i == IndiceSeleccionado;

            var itemGrid = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Padding = new Thickness(14, 12),
                BackgroundColor = seleccionado ? UIHelpers.GetColor("Primary") : Colors.Transparent,
            };

            itemGrid.Add(new Label
            {
                Text = texto,
                FontSize = 14,
                TextColor = UIHelpers.GetColor("PrimaryText"),
                FontAttributes = seleccionado ? FontAttributes.Bold : FontAttributes.None,
                VerticalOptions = LayoutOptions.Center,
            });

            if (seleccionado)
            {
                itemGrid.Add(new Label
                {
                    Text = Fonts.FluentUI.checkmark_20_regular,
                    FontFamily = Fonts.FluentUI.FontFamily,
                    FontSize = 16,
                    TextColor = UIHelpers.GetColor("PrimaryText"),
                    VerticalOptions = LayoutOptions.Center,
                }, 1);
            }

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SeleccionarIndice(indice);
            itemGrid.GestureRecognizers.Add(tap);

            lista.Add(itemGrid);
        }

        const double altoItem = 42;
        double altoEstimado = elementos.Count * altoItem;
        View contenido;

        if (altoEstimado > MaxAltoLista)
        {
            contenido = new ScrollView
            {
                Content = lista,
                HeightRequest = MaxAltoLista,
            };
        }
        else
        {
            contenido = lista;
        }

        await OverlayFlotante.MostrarEnPagina(Trigger, contenido, Trigger.Width);
    }

    private void SeleccionarIndice(int indice)
    {
        _sincronizando = true;
        try
        {
            IndiceSeleccionado = indice;
            if (Elementos is not null && indice >= 0 && indice < Elementos.Count)
                ElementoSeleccionado = Elementos[indice];
        }
        finally
        {
            _sincronizando = false;
        }

        OverlayFlotante.Ocultar();
        ActualizarTexto();
        IndiceCambiado?.Invoke(this, indice);
        SeleccionCambiadaCommand?.Execute(ElementoSeleccionado);
    }

    private void ActualizarTexto()
    {
        var elementos = Elementos;
        var indice = IndiceSeleccionado;

        if (elementos is not null && indice >= 0 && indice < elementos.Count)
        {
            LabelTexto.Text = elementos[indice]?.ToString() ?? string.Empty;
            LabelTexto.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
        }
        else
        {
            LabelTexto.Text = Titulo;
            LabelTexto.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
        }
    }

    private static void OnAparienciaChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SelectorFlotante selector)
            selector.ActualizarTexto();
    }

    private static void OnSeleccionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SelectorFlotante selector && !selector._sincronizando)
        {
            var indice = (int)newValue;
            if (selector.Elementos is not null && indice >= 0 && indice < selector.Elementos.Count)
                selector.ElementoSeleccionado = selector.Elementos[indice];
            selector.ActualizarTexto();
        }
    }

    private static void OnElementoSeleccionadoChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SelectorFlotante selector && !selector._sincronizando && selector.Elementos is not null)
        {
            for (int i = 0; i < selector.Elementos.Count; i++)
            {
                if (Equals(selector.Elementos[i], newValue))
                {
                    selector._sincronizando = true;
                    selector.IndiceSeleccionado = i;
                    selector._sincronizando = false;
                    break;
                }
            }
            selector.ActualizarTexto();
        }
    }
}
