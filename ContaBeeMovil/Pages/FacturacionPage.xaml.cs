using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using ContaBeeMovil.Config;
using ContaBeeMovil.Helpers;
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
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioLogs _logs;
    private Contabee.Api.Transcript.Busqueda? _ultimaBusqueda;

    // Página 1 de cada combinación de filtros se cachea: se pinta al instante y se refresca en
    // segundo plano. Las búsquedas de un solo uso (por proceso o por ventana de envío) no.
    private bool _cachearBusqueda;
    private sealed record PaginaCacheada(List<ElementoPaginaCapturaDespliegue> Elementos, long Total);

    // Cada búsqueda toma un número; una respuesta que llega cuando el usuario ya cambió de
    // filtro o de página se guarda en caché pero no se pinta.
    private int _versionBusqueda;
    internal static Guid? ProcesoAsociadoFiltroId { get; set; }
    internal static TipoProcesoCaptura? ProcesoAsociadoFiltroTipo { get; set; }
    internal static DateTimeOffset? CapturaRecienCreadaFiltroFecha { get; set; }

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

    // Habilita la captura si hay créditos de CUALQUIER tipo (Captura o Autoservicio);
    // PaginaCaptura ya resuelve cuál usar y bloquea el envío si se agotan.
    public bool TieneCreditos =>
        (AppState.Instance.Licenciamiento?.CreditosDisponibles ?? 0) > 0 ||
        (AppState.Instance.Licenciamiento?.CreditosAutoDisponibles ?? 0) > 0;

    public bool SinCreditos => !TieneCreditos;

    /// <summary>Panel de filtros expuesto para sincronización de título desde MainTabbedPage.</summary>
    public FiltrosFacturasView Filtros => PanelFiltros;

    // ── Comandos ─────────────────────────────────────────────────────────────────

    public ICommand BuscarFacturasCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }

    // ── Constructor ──────────────────────────────────────────────────────────────

    public FacturacionPage(IServicioTranscript servicioTranscript, IServicioAlerta servicioAlerta, IServicioSesion servicioSesion, IServicioLogs logs)
    {
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _servicioSesion = servicioSesion;
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
        };
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────────

    internal static bool PendienteActualizarFacturas { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (AppState.Instance.MisUsuarios is null || AppState.Instance.MisUsuarios.Count == 0)
            await _servicioSesion.GetMisUsuariosAsync();

        PanelFiltros.RestaurarEstado();
    }

    private static Contabee.Api.Transcript.Busqueda CrearBusquedaPorFechaCreacion(DateTimeOffset fechaEnvio)
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

        // Ventana alrededor del momento del envío (en UTC, mismo formato que IrARecientes).
        // Margen amplio para tolerar diferencia de reloj dispositivo/servidor.
        static string Fmt(DateTimeOffset d) => d.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") + "Z";
        var desde = Fmt(fechaEnvio.AddMinutes(-3));
        var hasta = Fmt(fechaEnvio.AddMinutes(10));

        filtros.Add(new Contabee.Api.Transcript.Filtro
        {
            Propiedad = "FechaCreacion",
            Operador = Operador.Entre,
            Valores = [desde, hasta]
        });

        return new Contabee.Api.Transcript.Busqueda
        {
            Filtros = filtros,
            OrdernarDesc = true,
            OrdenarPropiedad = "FechaCreacion",
            Paginado = new Contabee.Api.Transcript.Paginado { Pagina = 1, TamanoPagina = AppSettings.Consulta.TamanoPagina },
            Contar = true
        };
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

    public async void OnTabActivated()
    {
        PanelFiltros.RestaurarEstado();

        if (AppState.Instance.MisUsuarios is null || AppState.Instance.MisUsuarios.Count == 0)
            await _servicioSesion.GetMisUsuariosAsync();

        if (ProcesoAsociadoFiltroId.HasValue)
        {
            var busquedaProceso = CrearBusquedaPorProceso(ProcesoAsociadoFiltroId.Value, ProcesoAsociadoFiltroTipo);
            ProcesoAsociadoFiltroId = null;
            ProcesoAsociadoFiltroTipo = null;
            await OnBuscarFacturas(busquedaProceso, cachear: false);
            return;
        }

        // Tras crear una captura, mostrar únicamente las capturas recién creadas
        // (filtro por ventana de fecha de creación alrededor del momento del envío).
        if (CapturaRecienCreadaFiltroFecha.HasValue)
        {
            var fechaEnvio = CapturaRecienCreadaFiltroFecha.Value;
            CapturaRecienCreadaFiltroFecha = null;
            PendienteActualizarFacturas = false;
            await OnBuscarFacturas(CrearBusquedaPorFechaCreacion(fechaEnvio), cachear: false);
            return;
        }

        if (!PendienteActualizarFacturas) return;
        PendienteActualizarFacturas = false;
        await Task.Delay(250);
        PanelFiltros.IrARecientes();
    }

    /// <summary>
    /// Carga en segundo plano la búsqueda con los filtros guardados, para que al abrir la
    /// pestaña ya haya datos (los pods de Azure tardan en despertar). La llama
    /// <see cref="MainTabbedPage"/>; no hace nada si ya hay consulta o una búsqueda dirigida
    /// pendiente (tras captura / por proceso), que resuelve <see cref="OnTabActivated"/>.
    /// </summary>
    public async Task PrecargarAsync()
    {
        if (ConsultaEjecutada || EstaCargando || PendienteActualizarFacturas) return;
        if (ProcesoAsociadoFiltroId.HasValue || CapturaRecienCreadaFiltroFecha.HasValue) return;
        if (AppState.Instance.ModoOffline || AppState.Instance.CuentaFiscalActual is null) return;

        _logs.Log("[FacturacionPage] Precarga");
        PanelFiltros.RestaurarEstado();
        if (AppState.Instance.MisUsuarios is null || AppState.Instance.MisUsuarios.Count == 0)
            await _servicioSesion.GetMisUsuariosAsync();
        await OnBuscarFacturas(PanelFiltros.BusquedaActual);
    }

    /// <summary>
    /// Invalida los resultados cacheados (que pertenecen a la cuenta fiscal anterior)
    /// y deja la página marcada para recargar. La llama <see cref="MainTabbedPage"/>
    /// al detectar un cambio de cuenta fiscal activa.
    /// </summary>
    public void InvalidarConsulta()
    {
        _versionBusqueda++; // una respuesta en vuelo de la cuenta anterior ya no se pinta
        EstaCargando = false;
        _ultimaBusqueda = null;
        Elementos = null;
        TotalEncontrados = 0;
        PaginaActual = 1;
        TotalPaginas = 1;
        ConsultaEjecutada = false;
        PendienteActualizarFacturas = true;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────────

    private async void OnAbrirCaptura(object sender, TappedEventArgs e)
    {
        if (!TieneCreditos) return;
        await Shell.Current.GoToAsync(nameof(PaginaCaptura),
            new Dictionary<string, object> { ["tipo"] = TipoProcesoCaptura.FacturaIndividual });
    }


    private async Task OnBuscarFacturas(Contabee.Api.Transcript.Busqueda busqueda, bool cachear = true)
    {
        if (AppState.Instance.CuentaFiscalActual is null)
        {
            // Sin cuenta seleccionada: solo ofrecemos el selector si hay cuentas. En modo
            // limitado (0 cuentas) no hay nada que elegir → no abrimos un popup vacío.
            if (AppState.Instance.CuentasFiscales is { Count: > 0 })
                await this.ShowPopupAsync(new CuentaFiscalSelectorPopup());
            return;
        }

        _ultimaBusqueda = busqueda;
        _cachearBusqueda = cachear;
        await EjecutarBusqueda(1);
    }

    private async Task EjecutarBusqueda(int pagina)
    {
        if (_ultimaBusqueda is null) return;

        _ultimaBusqueda.Paginado = new Contabee.Api.Transcript.Paginado { Pagina = pagina, TamanoPagina = AppSettings.Consulta.TamanoPagina };

        var version = ++_versionBusqueda;
        bool esVigente() => version == _versionBusqueda;

        // Solo la página 1: es la que se ve al entrar; las siguientes llegan rápido porque la
        // primera consulta ya despertó los pods.
        var cfid = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        var clave = _cachearBusqueda && pagina == 1 && cfid is Guid id
            ? CacheConsultas.Clave(CacheConsultas.Facturacion, id, _ultimaBusqueda)
            : null;

        var cache = clave is null ? null : CacheConsultas.Leer<PaginaCacheada>(clave);

        // El esqueleto sale siempre como respuesta al toque. Con caché solo dura PausaVisual
        // y el refresco sigue en silencio; sin caché dura lo que tarde el servidor.
        EstaCargando = true;
        var consulta = _servicioTranscript.BusquedaCapturas(_ultimaBusqueda);
        if (cache != null)
        {
            await Task.Delay(CacheConsultas.PausaVisual);
            if (esVigente())
            {
                AplicarResultado(cache.Datos.Elementos, cache.Datos.Total, pagina);
                EstaCargando = false;
            }
        }

        try
        {
            var resultado = await consulta;
            var elementosPagina = resultado.Elementos?.ToList() ?? [];

            if (clave != null)
                CacheConsultas.Guardar(clave, new PaginaCacheada(elementosPagina, resultado.Total));

            if (!esVigente()) return;
            AplicarResultado(elementosPagina, resultado.Total, pagina);
        }
        catch (ApiException ex)
        {
            _logs.Log($"[FacturacionPage] ApiException Status={ex.StatusCode} Body={ex.Response} (caché={cache != null})");
            if (!esVigente()) return;

            // Asociación desactivada/eliminada (403): el CoordinadorSesion reconcilia la
            // sesión y avisa con toast. Aquí solo dejamos el listado vacío; sin error genérico.
            if (CodigosErrorApi.EsAsociacionInactiva((System.Net.HttpStatusCode)ex.StatusCode, ex.Response))
            {
                Elementos = [];
                TotalEncontrados = 0;
                ConsultaEjecutada = true;
                return;
            }

            // Con caché en pantalla no interrumpimos con una alerta: el usuario ya ve datos.
            if (cache == null)
                await _servicioAlerta.MostrarAsync("Error", "No se pudieron cargar los resultados. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logs.Log($"[FacturacionPage] {ex.GetType().Name}: {ex.Message} (caché={cache != null})");
            if (esVigente() && cache == null)
                await _servicioAlerta.MostrarAsync("Error", "No se pudieron cargar los resultados. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
        }
        finally
        {
            // Si ya hay otra búsqueda en curso, esa maneja su propio overlay.
            if (esVigente())
                EstaCargando = false;
        }
    }

    private void AplicarResultado(List<ElementoPaginaCapturaDespliegue> elementosPagina, long total, int pagina)
    {
        int offset = (pagina - 1) * AppSettings.Consulta.TamanoPagina;
        Elementos = elementosPagina
            .Select((e, i) => new ItemConConsecutivo(offset + i + 1, e))
            .ToList();
        TotalEncontrados = total;
        PaginaActual = pagina;
        TotalPaginas = (int)Math.Ceiling((double)total / AppSettings.Consulta.TamanoPagina);
        if (TotalPaginas < 1) TotalPaginas = 1;
        ConsultaEjecutada = true;
    }
}
