using System.Collections;
using System.Windows.Input;
using ContaBeeMovil.Config;

namespace ContaBeeMovil.Views;

public partial class ResultadosListaView : ContentView
{
    // ── BindableProperties ───────────────────────────────────────────────────────

    public static readonly BindableProperty ElementosProperty =
        BindableProperty.Create(nameof(Elementos), typeof(IEnumerable), typeof(ResultadosListaView),
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).Lista.ItemsSource = v as IEnumerable);

    public IEnumerable? Elementos
    {
        get => (IEnumerable?)GetValue(ElementosProperty);
        set => SetValue(ElementosProperty, value);
    }

    public static readonly BindableProperty EstaCargandoProperty =
        BindableProperty.Create(nameof(EstaCargando), typeof(bool), typeof(ResultadosListaView), false,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).LoadingOverlay.IsVisible = (bool)v);

    public bool EstaCargando
    {
        get => (bool)GetValue(EstaCargandoProperty);
        set => SetValue(EstaCargandoProperty, value);
    }

    public static readonly BindableProperty TotalEncontradosProperty =
        BindableProperty.Create(nameof(TotalEncontrados), typeof(long), typeof(ResultadosListaView), 0L,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).ActualizarPaginacion());

    public long TotalEncontrados
    {
        get => (long)GetValue(TotalEncontradosProperty);
        set => SetValue(TotalEncontradosProperty, value);
    }

    public static readonly BindableProperty PaginaActualProperty =
        BindableProperty.Create(nameof(PaginaActual), typeof(int), typeof(ResultadosListaView), 1,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).ActualizarPaginacion());

    public int PaginaActual
    {
        get => (int)GetValue(PaginaActualProperty);
        set => SetValue(PaginaActualProperty, value);
    }

    public static readonly BindableProperty TotalPaginasProperty =
        BindableProperty.Create(nameof(TotalPaginas), typeof(int), typeof(ResultadosListaView), 1,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).ActualizarPaginacion());

    public int TotalPaginas
    {
        get => (int)GetValue(TotalPaginasProperty);
        set => SetValue(TotalPaginasProperty, value);
    }

    public static readonly BindableProperty PlantillaItemProperty =
        BindableProperty.Create(nameof(PlantillaItem), typeof(DataTemplate), typeof(ResultadosListaView),
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).Lista.ItemTemplate = v as DataTemplate);

    public DataTemplate? PlantillaItem
    {
        get => (DataTemplate?)GetValue(PlantillaItemProperty);
        set => SetValue(PlantillaItemProperty, value);
    }

    public static readonly BindableProperty ConsultaEjecutadaProperty =
        BindableProperty.Create(nameof(ConsultaEjecutada), typeof(bool), typeof(ResultadosListaView), false,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).ActualizarPaginacion());

    public bool ConsultaEjecutada
    {
        get => (bool)GetValue(ConsultaEjecutadaProperty);
        set => SetValue(ConsultaEjecutadaProperty, value);
    }

    public static readonly BindableProperty AnteriorCommandProperty =
        BindableProperty.Create(nameof(AnteriorCommand), typeof(ICommand), typeof(ResultadosListaView));

    public ICommand? AnteriorCommand
    {
        get => (ICommand?)GetValue(AnteriorCommandProperty);
        set => SetValue(AnteriorCommandProperty, value);
    }

    public static readonly BindableProperty SiguienteCommandProperty =
        BindableProperty.Create(nameof(SiguienteCommand), typeof(ICommand), typeof(ResultadosListaView));

    public ICommand? SiguienteCommand
    {
        get => (ICommand?)GetValue(SiguienteCommandProperty);
        set => SetValue(SiguienteCommandProperty, value);
    }

    public static readonly BindableProperty CapturaCommandProperty =
        BindableProperty.Create(nameof(CapturaCommand), typeof(ICommand), typeof(ResultadosListaView));

    public ICommand? CapturaCommand
    {
        get => (ICommand?)GetValue(CapturaCommandProperty);
        set => SetValue(CapturaCommandProperty, value);
    }

    public static readonly BindableProperty MostrarBotonCapturaProperty =
        BindableProperty.Create(nameof(MostrarBotonCaptura), typeof(bool), typeof(ResultadosListaView), false,
            propertyChanged: (b, _, v) => ((ResultadosListaView)b).BtnCaptura.IsVisible = (bool)v);

    public bool MostrarBotonCaptura
    {
        get => (bool)GetValue(MostrarBotonCapturaProperty);
        set => SetValue(MostrarBotonCapturaProperty, value);
    }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public ResultadosListaView()
    {
        InitializeComponent();
        ActualizarPaginacion();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────

    private void OnAnteriorTapped(object sender, TappedEventArgs e)
    {
        if (AnteriorCommand?.CanExecute(null) == true)
            AnteriorCommand.Execute(null);
    }

    private void OnSiguienteTapped(object sender, TappedEventArgs e)
    {
        if (SiguienteCommand?.CanExecute(null) == true)
            SiguienteCommand.Execute(null);
    }

    private void OnCapturaTapped(object sender, TappedEventArgs e)
    {
        if (CapturaCommand?.CanExecute(null) == true)
            CapturaCommand.Execute(null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void ActualizarPaginacion()
    {
        BarraPaginacion.IsVisible = ConsultaEjecutada;

        LabelPagina.Text = $"{PaginaActual}";
        LabelTotal.Text = $"Encontrados {TotalEncontrados}";

        bool anteriorActivo = PaginaActual > 1;
        BtnAnterior.Opacity = anteriorActivo ? 1.0 : 0.3;
        BtnAnterior.IsEnabled = anteriorActivo;

        bool hayMasDeUnaPagina = TotalEncontrados > AppSettings.Consulta.TamanoPagina;
        bool siguienteActivo = hayMasDeUnaPagina && PaginaActual < TotalPaginas;
        BtnSiguiente.Opacity = siguienteActivo ? 1.0 : 0.3;
        BtnSiguiente.IsEnabled = siguienteActivo;
    }
}
