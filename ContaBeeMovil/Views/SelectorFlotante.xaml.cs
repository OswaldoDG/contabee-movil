using MauiIcons.Core;
using MauiIcons.Material;
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

    public static readonly BindableProperty UsarSelectorModalProperty =
        BindableProperty.Create(nameof(UsarSelectorModal), typeof(bool), typeof(SelectorFlotante), defaultValue: false);

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

    public bool UsarSelectorModal
    {
        get => (bool)GetValue(UsarSelectorModalProperty);
        set => SetValue(UsarSelectorModalProperty, value);
    }

    public event EventHandler<int>? IndiceCambiado;

    private bool _sincronizando;
    private bool _dropdownEmbebidoVisible;

    public SelectorFlotante()
    {
        InitializeComponent();
        ActualizarTexto();
    }

    private async void OnTriggerTapped(object? sender, TappedEventArgs e)
    {
        if (UsarSelectorModal)
        {
            await MostrarSelectorModalAsync();
            return;
        }

        if (EstaDentroDePopup())
        {
            await ToggleDropdownEmbebidoAsync();
            return;
        }

        if (OverlayFlotante.EstaVisible)
        {
            OverlayFlotante.Ocultar();
            return;
        }

        if (IndiceSeleccionado < 0
            && Elementos is { Count: > 0 })
        {
            SeleccionarIndice(0);
        }

        _ = MostrarDropdown();
    }

    private async Task ToggleDropdownEmbebidoAsync()
    {
        if (_dropdownEmbebidoVisible)
        {
            await OcultarDropdownEmbebidoAsync();
            return;
        }

        await MostrarDropdownEmbebidoAsync();
    }

    private async Task MostrarDropdownEmbebidoAsync()
    {
        var elementos = Elementos;
        if (elementos is null || elementos.Count == 0)
            return;

        ListaOpciones.Children.Clear();

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
            tap.Tapped += async (_, _) =>
            {
                SeleccionarIndice(indice);
                await OcultarDropdownEmbebidoAsync();
            };
            itemGrid.GestureRecognizers.Add(tap);

            ListaOpciones.Children.Add(itemGrid);
        }

        const double altoItem = 42;
        var altoTotal = elementos.Count * altoItem;
        var altoMaximo = Math.Max(84, MaxAltoLista);
        ScrollOpciones.HeightRequest = Math.Min(altoTotal, altoMaximo);

        PanelOpciones.IsVisible = true;
        await PanelOpciones.FadeToAsync(1, 120, Easing.CubicOut);
        _dropdownEmbebidoVisible = true;
    }

    private async Task OcultarDropdownEmbebidoAsync()
    {
        if (!_dropdownEmbebidoVisible)
            return;

        await PanelOpciones.FadeToAsync(0, 100, Easing.CubicIn);
        PanelOpciones.IsVisible = false;
        _dropdownEmbebidoVisible = false;
    }

    private bool EstaDentroDePopup()
    {
        Element? actual = this;
        while (actual is not null)
        {
            if (actual is CommunityToolkit.Maui.Views.Popup)
                return true;
            actual = actual.Parent;
        }

        return false;
    }

    private async Task MostrarSelectorModalAsync()
    {
        if (Elementos is null || Elementos.Count == 0)
            return;

        if (Elementos.Count == 1)
        {
            SeleccionarIndice(0);
            return;
        }

        var pagina = ObtenerPaginaActiva() ?? this.ObtenerPagina();
        if (pagina is null)
        {
            if (IndiceSeleccionado < 0)
                SeleccionarIndice(0);
            return;
        }

        var opciones = new string[Elementos.Count];
        for (int i = 0; i < Elementos.Count; i++)
            opciones[i] = Elementos[i]?.ToString() ?? string.Empty;

        string? seleccionado;
        try
        {
            seleccionado = await MainThread.InvokeOnMainThreadAsync(() =>
                pagina.DisplayActionSheet(Titulo, "Cancelar", null, opciones));
        }
        catch
        {
            if (IndiceSeleccionado < 0)
                SeleccionarIndice(0);
            return;
        }

        if (string.IsNullOrWhiteSpace(seleccionado) || seleccionado == "Cancelar")
            return;

        var indice = Array.IndexOf(opciones, seleccionado);
        if (indice >= 0)
            SeleccionarIndice(indice);
    }

    private static Page? ObtenerPaginaActiva()
    {
        Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is Shell shell)
            page = shell.CurrentPage;

        if (page is NavigationPage navigationPage)
            page = navigationPage.CurrentPage;

        if (page is TabbedPage tabbedPage)
            page = tabbedPage.CurrentPage;

        return page;
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
                itemGrid.Add(new MauiIcon
                {
                    Icon = MaterialIcons.Done,
                    IconSize = 16,
                    IconColor = UIHelpers.GetColor("PrimaryText"),
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

        await OverlayFlotante.MostrarEnPagina(Trigger, contenido, Math.Max(Trigger.Width, 150));
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
