using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using CommunityToolkit.Mvvm.ComponentModel;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Captura;
using System.Windows.Input;

namespace ContaBeeMovil.Pages;

public partial class FacturacionPage : ContentPage
{
    private readonly IServicioTranscript _servicioTranscript;
    private Busqueda? _ultimaBusqueda;

    // ── Propiedades observables ──────────────────────────────────────────────────

    private bool _estaCargando;
    public bool EstaCargando
    {
        get => _estaCargando;
        private set { _estaCargando = value; OnPropertyChanged(); }
    }

    private IEnumerable<ItemConConsecutivo>? _elementos;
    public IEnumerable<ItemConConsecutivo>? Elementos
    {
        get => _elementos;
        private set { _elementos = value; OnPropertyChanged(); }
    }

    public record ItemConConsecutivo(int Consecutivo, ElementoPaginaCapturaDespliegue Datos);

    private long _totalEncontrados;
    public long TotalEncontrados
    {
        get => _totalEncontrados;
        private set { _totalEncontrados = value; OnPropertyChanged(); }
    }

    private int _paginaActual = 1;
    public int PaginaActual
    {
        get => _paginaActual;
        private set { _paginaActual = value; OnPropertyChanged(); }
    }

    private int _totalPaginas = 1;
    public int TotalPaginas
    {
        get => _totalPaginas;
        private set { _totalPaginas = value; OnPropertyChanged(); }
    }

    private bool _consultaEjecutada;
    public bool ConsultaEjecutada
    {
        get => _consultaEjecutada;
        private set { _consultaEjecutada = value; OnPropertyChanged(); }
    }

    // ── Comandos ─────────────────────────────────────────────────────────────────

    public ICommand BuscarFacturasCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public FacturacionPage(IServicioTranscript servicioTranscript)
    {
        _servicioTranscript = servicioTranscript;
        BuscarFacturasCommand = new Command<Busqueda>(async b => await OnBuscarFacturas(b));
        PaginaAnteriorCommand = new Command(async () => await EjecutarBusqueda(PaginaActual - 1));
        PaginaSiguienteCommand = new Command(async () => await EjecutarBusqueda(PaginaActual + 1));
        InitializeComponent();
        BindingContext = this;
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PanelFiltros.RestaurarEstado();
        // TODO: relanzar última consulta al regresar de PaginaCaptura
        //if (_ultimaBusqueda is null) return;
        //await Task.Delay(250);
        //await EjecutarBusqueda(PaginaActual);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────

    private async void OnAbrirCaptura(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(PaginaCaptura));

    private async Task OnBuscarFacturas(Busqueda busqueda)
    {
        _ultimaBusqueda = busqueda;
        await EjecutarBusqueda(1);
    }

    private async Task EjecutarBusqueda(int pagina)
    {
        if (_ultimaBusqueda is null) return;

        _ultimaBusqueda.Paginado = new Paginado { Pagina = pagina, TamanoPagina = AppSettings.Consulta.TamanoPagina };

        EstaCargando = true;
        try
        {
            var resultado = await _servicioTranscript.BusquedaCapturas(_ultimaBusqueda);

            int offset = (pagina - 1) * AppSettings.Consulta.TamanoPagina;
            Elementos = resultado.Elementos?
                .Select((e, i) => new ItemConConsecutivo(offset + i + 1, e))
                .ToList();
            TotalEncontrados = resultado.Total;
            PaginaActual = pagina;
            TotalPaginas = (int)Math.Ceiling((double)resultado.Total / AppSettings.Consulta.TamanoPagina);
            if (TotalPaginas < 1) TotalPaginas = 1;
            ConsultaEjecutada = true;

        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
