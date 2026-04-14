using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;

namespace ContaBeeMovil.Pages;

public partial class FacturacionPage : ContentPage
{
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
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

    public bool TieneCreditos =>
        (AppState.Instance.Licenciamiento?.CreditosCaptura ?? 0) > 0;

    public bool SinCreditos => !TieneCreditos;

    // ── Comandos ─────────────────────────────────────────────────────────────────

    public ICommand BuscarFacturasCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public FacturacionPage(IServicioTranscript servicioTranscript, IServicioAlerta servicioAlerta)
    {
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        BuscarFacturasCommand = new Command<Busqueda>(async b => await OnBuscarFacturas(b));
        PaginaAnteriorCommand = new Command(async () => await EjecutarBusqueda(PaginaActual - 1));
        PaginaSiguienteCommand = new Command(async () => await EjecutarBusqueda(PaginaActual + 1));
        InitializeComponent();
        BindingContext = this;

        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.Licenciamiento))
            {
                OnPropertyChanged(nameof(TieneCreditos));
                OnPropertyChanged(nameof(SinCreditos));
            }
            if (e.PropertyName is nameof(AppState.MisUsuarios))
                ActualizarCreadores();
        };
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────────

    private void ActualizarCreadores()
    {
        var usuarios = AppState.Instance.MisUsuarios ?? [];
        PanelFiltros.Creadores  = ["Creador",  .. usuarios.Select(u => u.Nombre ?? u.Email ?? u.Id.ToString())];
        PanelFiltros.CreadoresIds = ["todos", .. usuarios.Select(u => u.Id.ToString())];
    }

    internal static bool PendienteActualizarFacturas { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ActualizarCreadores();
        PanelFiltros.RestaurarEstado();
        if (!PendienteActualizarFacturas) return;
        PendienteActualizarFacturas = false;
        await Task.Delay(250);
        PanelFiltros.IrARecientes();
    }

    public void OnTabActivated()
    {
        Dispatcher.Dispatch(() =>
        {
            ActualizarCreadores();
            PanelFiltros.RestaurarEstado();
        });
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────

    private async void OnAbrirCaptura(object sender, TappedEventArgs e)
    {
        if (!TieneCreditos) return;
        await Shell.Current.GoToAsync(nameof(PaginaCaptura),
            new Dictionary<string, object> { ["tipo"] = TipoProcesoCaptura.FacturaIndividual });
    }

    private async Task OnBuscarFacturas(Busqueda busqueda)
    {
        if (AppState.Instance.CuentaFiscalActual is null)
        {
            await this.ShowPopupAsync(new CuentaFiscalSelectorPopup());
            return;
        }

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
            await _servicioAlerta.MostrarAsync("Error", ex.Message, verBotonCancelar: false, confirmarText: "OK");
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
