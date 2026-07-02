using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Devoluciones;

public partial class DetalleDevolucionPage : ContentPage, IQueryAttributable
{
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;

    private Guid _devolucionId;
    private string _rfc = "RFC no disponible";
    private Devolucion? _devolucion;
    private bool _navegandoTrasEliminacion;
    private bool _detalleNoDisponible;

    public bool EstaCargando
    {
        get => _estaCargando;
        set { _estaCargando = value; OnPropertyChanged(); }
    }
    private bool _estaCargando;

    public string Rfc => _rfc;
    public string EstadoTexto => _devolucion is null ? "-" : ObtenerEstadoTexto(_devolucion.Estado);
    public string MontoTexto => (_devolucion?.Monto ?? 0d).ToString("C2");
    public string CreacionTexto => _devolucion?.Creacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "-";
    public string DescripcionTexto => string.IsNullOrWhiteSpace(_devolucion?.Descripcion) ? "Sin descripción" : _devolucion!.Descripcion!;

    public bool PuedeActualizarEstado => EsUsuarioPrimario && EstadosDisponibles.Count > 0;
    public bool EsUsuarioPrimario
    {
        get
        {
            var cuentaActual = AppState.Instance.CuentaFiscalActual;
            if (cuentaActual is null)
                return true;

            return !EsCuentaSecundaria(cuentaActual.TipoCuenta.ToString());
        }
    }

    public ObservableCollection<string> EstadosDisponibles { get; } = [];

    public IEnumerable<FacturacionPage.ItemConConsecutivo>? CapturasRelacionadas
    {
        get => _capturasRelacionadas;
        private set { _capturasRelacionadas = value; OnPropertyChanged(); OnPropertyChanged(nameof(TieneCapturasRelacionadas)); }
    }
    private IEnumerable<FacturacionPage.ItemConConsecutivo>? _capturasRelacionadas;

    public bool TieneCapturasRelacionadas => CapturasRelacionadas?.Any() == true;

    public long TotalCapturasEncontradas
    {
        get => _totalCapturasEncontradas;
        private set { _totalCapturasEncontradas = value; OnPropertyChanged(); }
    }
    private long _totalCapturasEncontradas;

    public int PaginaCapturasActual
    {
        get => _paginaCapturasActual;
        private set { _paginaCapturasActual = value; OnPropertyChanged(); }
    }
    private int _paginaCapturasActual = 1;

    public int TotalPaginasCapturas
    {
        get => _totalPaginasCapturas;
        private set { _totalPaginasCapturas = value; OnPropertyChanged(); }
    }
    private int _totalPaginasCapturas = 1;

    public bool ConsultaCapturasEjecutada
    {
        get => _consultaCapturasEjecutada;
        private set { _consultaCapturasEjecutada = value; OnPropertyChanged(); }
    }
    private bool _consultaCapturasEjecutada;

    public string? EstadoSeleccionado
    {
        get => _estadoSeleccionado;
        set { _estadoSeleccionado = value; OnPropertyChanged(); }
    }
    private string? _estadoSeleccionado;

    public ICommand ActualizarEstadoCommand { get; }
    public ICommand IrCapturaCommand { get; }
    public ICommand PaginaCapturasAnteriorCommand { get; }
    public ICommand PaginaCapturasSiguienteCommand { get; }

    public DetalleDevolucionPage(
        IServicioTranscript servicioTranscript,
        IServicioAlerta servicioAlerta,
        IServicioLogs logs)
    {
        InitializeComponent();
        InicializarSelectores();
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _logs = logs;

        ActualizarEstadoCommand = new Command(async () => await ActualizarEstadoAsync());
        IrCapturaCommand = new Command(async () => await IrCapturaAsync());
        PaginaCapturasAnteriorCommand = new Command(async () => await CargarCapturasRelacionadasAsync(PaginaCapturasActual - 1));
        PaginaCapturasSiguienteCommand = new Command(async () => await CargarCapturasRelacionadasAsync(PaginaCapturasActual + 1));

        BindingContext = this;
    }

    private void InicializarSelectores() 
    {
        SelectorEstado.Elementos = EstadosDisponibles;
        SelectorEstado.IndiceSeleccionado = 0;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("devolucionId", out var idObj) && idObj is Guid id)
            _devolucionId = id;

        if (query.TryGetValue("rfc", out var rfcObj) && rfcObj is string rfc && !string.IsNullOrWhiteSpace(rfc))
            _rfc = rfc;

        OnPropertyChanged(nameof(Rfc));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_navegandoTrasEliminacion || _detalleNoDisponible)
            return;

        await CargarDetalleAsync();
    }

    private async Task CargarDetalleAsync()
    {
        if (_devolucionId == Guid.Empty) return;
        if (_navegandoTrasEliminacion || _detalleNoDisponible) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.ObtenerDevolucionAsync(_devolucionId);
            if (!res.Ok || res.Payload is null)
            {
                if (EsEntidadNoEncontrada(res.Error?.Mensaje))
                {
                    _detalleNoDisponible = true;
                    PaginaDevoluciones.PendienteActualizarListado = true;
                    await RegresarAListadoSiSigueAbiertaAsync();
                    return;
                }

                // Asociación desactivada/eliminada (403): el CoordinadorSesion reconcilia la
                // sesión y avisa con toast; aquí solo volvemos al listado (sin error genérico).
                if (CodigosErrorApi.EsAsociacionInactiva(res.Error?.HttpCode, res.Error?.Mensaje))
                {
                    _detalleNoDisponible = true;
                    PaginaDevoluciones.PendienteActualizarListado = true;
                    await RegresarAListadoSiSigueAbiertaAsync();
                    return;
                }

                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo obtener el reembolso.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _devolucion = res.Payload;
            ConfigurarEstadosDisponibles();
            await CargarCapturasRelacionadasAsync(1);
            RefrescarBindings();
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private void RefrescarBindings()
    {
        OnPropertyChanged(nameof(EstadoTexto));
        OnPropertyChanged(nameof(MontoTexto));
        OnPropertyChanged(nameof(CreacionTexto));
        OnPropertyChanged(nameof(DescripcionTexto));
        OnPropertyChanged(nameof(PuedeActualizarEstado));
        OnPropertyChanged(nameof(EsUsuarioPrimario));
    }

    private void ConfigurarEstadosDisponibles()
    {
        EstadosDisponibles.Clear();
        if (_devolucion is null)
        {
            EstadoSeleccionado = null;
            return;
        }

        if (EsEstado(_devolucion.Estado, "Admitida", "Abierta", "Abrir"))
        {
            EstadosDisponibles.Add("Aceptar");
            EstadosDisponibles.Add("Declinar");
        }
        else if (EsEstado(_devolucion.Estado, "Creada"))
        {
            EstadosDisponibles.Add("Abrir");
        }
        else if (EsEstado(_devolucion.Estado, "Aceptada", "Aceptado", "Declinada", "Declinado"))
        {
            EstadosDisponibles.Add("Abrir");
        }

        EstadoSeleccionado = EstadosDisponibles.FirstOrDefault();
    }

    private async Task CargarCapturasRelacionadasAsync(int pagina)
    {
        const int tamanoPaginaCapturas = 5;

        if (_devolucion is null)
        {
            CapturasRelacionadas = [];
            TotalCapturasEncontradas = 0;
            PaginaCapturasActual = 1;
            TotalPaginasCapturas = 1;
            ConsultaCapturasEjecutada = true;
            return;
        }

        if (pagina < 1)
            return;

        try
        {
            var filtros = new List<Filtro>();

            var cuentaFiscalId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
            if (cuentaFiscalId.HasValue)
            {
                filtros.Add(new Filtro
                {
                    Propiedad = "CuentaFiscalId",
                    Operador = Operador.Igual,
                    Valores = [cuentaFiscalId.Value.ToString()]
                });
            }

            filtros.Add(new Filtro
            {
                Propiedad = "ProcesoAsociadoId",
                Operador = Operador.Igual,
                Valores = [_devolucion.Id.ToString()]
            });

            var busqueda = new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = true,
                OrdenarPropiedad = "FechaCreacion",
                Paginado = new Paginado { Pagina = pagina, TamanoPagina = tamanoPaginaCapturas },
                Contar = false
            };

            _logs.Log($"[DetalleDevolucionPage] BusquedaCapturas filtros={string.Join(" | ", filtros.Select(f => $"{f.Propiedad}:{f.Operador}=[{string.Join(",", f.Valores ?? [])}]"))}");

            var resultado = await _servicioTranscript.BusquedaCapturas(busqueda);
            CapturasRelacionadas = resultado.Elementos?
                .Select((e, i) => new FacturacionPage.ItemConConsecutivo(((pagina - 1) * tamanoPaginaCapturas) + i + 1, e))
                .ToList() ?? [];

            var total = resultado.Total > 0 ? resultado.Total : CapturasRelacionadas.Count();
            TotalCapturasEncontradas = total;
            PaginaCapturasActual = pagina;
            TotalPaginasCapturas = (int)Math.Ceiling((double)total / Math.Max(1, tamanoPaginaCapturas));
            if (TotalPaginasCapturas < 1)
                TotalPaginasCapturas = 1;
            ConsultaCapturasEjecutada = true;
        }
        catch (Exception ex)
        {
            _logs.Log($"[DetalleDevolucionPage] Error cargando capturas relacionadas: {ex.Message}");
            CapturasRelacionadas = [];
            TotalCapturasEncontradas = 0;
            PaginaCapturasActual = 1;
            TotalPaginasCapturas = 1;
            ConsultaCapturasEjecutada = true;
        }
    }

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (_devolucion is null) return;

        ActualizarDevolucionPopup.ResultadoActualizar? result = null;
        await this.ShowPopupAsync(new ActualizarDevolucionPopup(_devolucion, _rfc, r => result = r));
        if (result is null) return;

        EstaCargando = true;
        try
        {
            var req = new ActualizaDevolucion
            {
                Id = _devolucion.Id,
                Descripcion = result.Descripcion
            };

            var res = await _servicioTranscript.ActualizarDevolucionAsync(_devolucion.Id, req);
            if (!res.Ok || res.Payload is null)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo actualizar el reembolso.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _devolucion = res.Payload;
            RefrescarBindings();
            PaginaDevoluciones.PendienteActualizarListado = true;
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if (_devolucion is null) return;

        var confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar reembolso",
            "¿Deseas eliminar este reembolso?",
            confirmarText: "Eliminar",
            cancelarText: "Cancelar");

        if (!confirmar) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.EliminarDevolucionAsync(_devolucion.Id);
            if (!res.Ok)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo eliminar el reembolso.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            PaginaDevoluciones.PendienteActualizarListado = true;
            _navegandoTrasEliminacion = true;
            _detalleNoDisponible = true;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task RegresarAListadoSiSigueAbiertaAsync()
    {
        if (Shell.Current?.CurrentPage != this)
            return;

        await Shell.Current.GoToAsync("..");
    }

    private static bool EsEntidadNoEncontrada(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return false;

        return mensaje.Contains("no existe", StringComparison.OrdinalIgnoreCase)
            || mensaje.Contains("no encontrada", StringComparison.OrdinalIgnoreCase)
            || mensaje.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || mensaje.Contains("404", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ActualizarEstadoAsync()
    {
        if (_devolucion is null || string.IsNullOrWhiteSpace(EstadoSeleccionado)) return;

        if (!TryResolverEstadoSeleccionado(EstadoSeleccionado, out var estadoNuevo)) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.ActualizarEstadoDevolucionAsync(_devolucion.Id, estadoNuevo);
            if (!res.Ok || res.Payload is null)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo actualizar el estado.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _devolucion = res.Payload;
            ConfigurarEstadosDisponibles();
            RefrescarBindings();
            PaginaDevoluciones.PendienteActualizarListado = true;
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private static string ObtenerEstadoTexto(EstadoDevolucion estado)
    {
        if (EsEstado(estado, "Admitida", "Abierta", "Abrir"))
            return "Admitida";

        if (EsEstado(estado, "Creada"))
            return "Creada";

        return estado.ToString();
    }

    private static bool EsEstado(EstadoDevolucion estado, params string[] estados)
    {
        var valor = estado.ToString();
        return estados.Any(e => string.Equals(e, valor, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolverEstadoSeleccionado(string seleccionado, out EstadoDevolucion estado)
    {
        estado = default;
        switch (seleccionado)
        {
            case "Aceptar":
                return TryParseEstado("Aceptada", out estado)
                    || TryParseEstado("Aceptado", out estado);

            case "Declinar":
                return TryParseEstado("Declinada", out estado)
                    || TryParseEstado("Declinado", out estado);

            case "Abrir":
                return TryParseEstado("Admitida", out estado)
                    || TryParseEstado("Abierta", out estado)
                    || TryParseEstado("Abrir", out estado)
                    || TryParseEstado("Creada", out estado);

            default:
                return false;
        }
    }

    private static bool TryParseEstado(string valor, out EstadoDevolucion estado)
        => Enum.TryParse(valor, ignoreCase: true, out estado);

    private static bool EsCuentaSecundaria(string? tipoCuenta)
        => string.Equals(tipoCuenta, "Secundaria", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "Secondary", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "Empleado", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "EmpleadoCliente", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoCuenta, "UsuarioCaptura", StringComparison.OrdinalIgnoreCase);

    private async Task IrCapturaAsync()
    {
        if (_devolucion is null) return;

        FacturacionPage.ProcesoAsociadoFiltroId = _devolucion.Id;
        FacturacionPage.ProcesoAsociadoFiltroTipo = Contabee.Api.Transcript.TipoProcesoCaptura.Devolucion;
        await Shell.Current.GoToAsync(nameof(PaginaCaptura),
            new Dictionary<string, object>
            {
                ["tipo"] = Contabee.Api.Transcript.TipoProcesoCaptura.Devolucion,
                ["procesoId"] = _devolucion.Id
            });
    }
}
