using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;

namespace ContaBeeMovil.Pages;

public partial class FacturacionPage : ContentPage
{
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private Contabee.Api.Transcript.Busqueda? _ultimaBusqueda;
    internal static Guid? ProcesoAsociadoFiltroId { get; set; }
    internal static TipoProcesoCaptura? ProcesoAsociadoFiltroTipo { get; set; }

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

    /// <summary>Panel de filtros expuesto para sincronización de título desde MainTabbedPage.</summary>
    public FiltrosFacturasView Filtros => PanelFiltros;

    // ── Comandos ─────────────────────────────────────────────────────────────────

    public ICommand BuscarFacturasCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public FacturacionPage(IServicioTranscript servicioTranscript, IServicioAlerta servicioAlerta, IServicioLogs logs)
    {
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _logs = logs;
        BuscarFacturasCommand = new Command<Contabee.Api.Transcript.Busqueda>(async b => await OnBuscarFacturas(b));
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
        var secundarios = usuarios
            .Where(u => EsCuentaSecundaria(u.TipoCuenta) && u.CuentaFiscalId.HasValue)
            .Select(u => new
            {
                Usuario = u,
                CuentaFiscalId = u.CuentaFiscalId!.Value
            })
            .GroupBy(x => x.CuentaFiscalId)
            .Select(g => g.First().Usuario)
            .ToList();

        if (secundarios.Count == 0)
        {
            PanelFiltros.Creadores = ["Destinatarios"];
            PanelFiltros.CreadoresIds = [string.Empty];
            return;
        }

        PanelFiltros.Creadores =
        [
            "Destinatarios",
            .. secundarios.Select(u =>
            {
                var nombreUsuario = u.Nombre ?? u.UserName ?? u.Email ?? u.Id.ToString();
                return $"{nombreUsuario} (Secundaria)";
            })
        ];

        PanelFiltros.CreadoresIds =
        [
            string.Empty,
            .. secundarios.Select(u => u.CuentaFiscalId!.Value.ToString())
        ];
    }

    private static bool EsCuentaSecundaria(Contabee.Api.Identidad.TipoCuenta tipoCuenta)
        => tipoCuenta is Contabee.Api.Identidad.TipoCuenta.Empleado
            or Contabee.Api.Identidad.TipoCuenta.EmpleadoCliente
            or Contabee.Api.Identidad.TipoCuenta.UsuarioCaptura;

    internal static bool PendienteActualizarFacturas { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ActualizarCreadores();
        PanelFiltros.RestaurarEstado();

        if (ProcesoAsociadoFiltroId.HasValue)
        {
            var busquedaProceso = CrearBusquedaPorProceso(ProcesoAsociadoFiltroId.Value, ProcesoAsociadoFiltroTipo);
            ProcesoAsociadoFiltroId = null;
            ProcesoAsociadoFiltroTipo = null;
            await OnBuscarFacturas(busquedaProceso);
            return;
        }

        if (!PendienteActualizarFacturas) return;
        PendienteActualizarFacturas = false;
        await Task.Delay(250);
        PanelFiltros.IrARecientes();
    }

    private static Contabee.Api.Transcript.Busqueda CrearBusquedaPorProceso(Guid procesoId, TipoProcesoCaptura? tipo)
    {
        var filtros = new List<Contabee.Api.Transcript.Filtro>();
        var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (cuentaFiscalId.HasValue)
        {
            filtros.Add(new Contabee.Api.Transcript.Filtro
            {
                Propiedad = "CuentaFiscalId",
                Operador = Operador.Igual,
                Valores = [cuentaFiscalId.Value.ToString()]
            });
        }

        filtros.Add(new Contabee.Api.Transcript.Filtro
        {
            Propiedad = "ProcesoAsociadoId",
            Operador = Operador.Igual,
            Valores = [procesoId.ToString()]
        });

        if (tipo.HasValue)
        {
            filtros.Add(new Contabee.Api.Transcript.Filtro
            {
                Propiedad = "Tipo",
                Operador = Operador.Igual,
                Valores = [tipo.Value.ToString()]
            });
        }

        return new Contabee.Api.Transcript.Busqueda
        {
            Filtros = filtros,
            OrdernarDesc = true,
            OrdenarPropiedad = "FechaCreacion",
            Paginado = new Contabee.Api.Transcript.Paginado { Pagina = 1, TamanoPagina = AppSettings.Consulta.TamanoPagina },
            Contar = true
        };
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


    private async Task OnBuscarFacturas(Contabee.Api.Transcript.Busqueda busqueda)
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

        _ultimaBusqueda.Paginado = new Contabee.Api.Transcript.Paginado { Pagina = pagina, TamanoPagina = AppSettings.Consulta.TamanoPagina };

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
            _logs.Log($"[FacturacionPage] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error", "No se pudieron cargar los resultados. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
