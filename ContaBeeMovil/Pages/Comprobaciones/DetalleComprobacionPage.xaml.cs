using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using ContaBeeMovil.Pages;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Comprobaciones;

public partial class DetalleComprobacionPage : ContentPage, IQueryAttributable
{
    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;

    private Guid _comprobacionId;
    private string _rfc = "RFC no disponible";
    private Comprobacion? _comprobacion;
    private bool _navegandoTrasEliminacion;
    private bool _detalleNoDisponible;

    public bool EstaCargando
    {
        get => _estaCargando;
        set { _estaCargando = value; OnPropertyChanged(); }
    }
    private bool _estaCargando;

    public string Rfc => _rfc;
    public string CreacionTexto => _comprobacion?.Creacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "-";
    public string CierreTexto => (_comprobacion?.Cierre?.ToLocalTime().ToString("dd/MM/yyyy") ?? "-");
    public string RequeridoTexto => $"{_comprobacion?.PorcentajeCompropbar ?? 0}%";
    public string MontoTexto => (_comprobacion?.Monto ?? 0d).ToString("C2");
    public string MontoComprobadoTexto => (_comprobacion?.MontoComprobado ?? 0d).ToString("C2");
    public string ComprobadoPorcentajeTexto
    {
        get
        {
            if (_comprobacion is null || _comprobacion.Monto <= 0) return "0%";
            var valor = (_comprobacion.MontoComprobado / _comprobacion.Monto) * 100d;
            return $"{Math.Round(valor, 2)}%";
        }
    }
    public string DescripcionTexto => string.IsNullOrWhiteSpace(_comprobacion?.Descripcion) ? "Sin descripción" : _comprobacion!.Descripcion!;

    public bool PuedeActualizarEstado => EstadosDisponibles.Count > 0;

    public ObservableCollection<string> EstadosDisponibles { get; } = [];

    public IEnumerable<FacturacionPage.ItemConConsecutivo>? CapturasRelacionadas
    {
        get => _capturasRelacionadas;
        private set { _capturasRelacionadas = value; OnPropertyChanged(); OnPropertyChanged(nameof(TieneCapturasRelacionadas)); }
    }
    private IEnumerable<FacturacionPage.ItemConConsecutivo>? _capturasRelacionadas;

    public bool TieneCapturasRelacionadas => CapturasRelacionadas?.Any() == true;

    public string? EstadoSeleccionado
    {
        get => _estadoSeleccionado;
        set { _estadoSeleccionado = value; OnPropertyChanged(); }
    }
    private string? _estadoSeleccionado;

    public ICommand ActualizarEstadoCommand { get; }
    public ICommand IrCapturaCommand { get; }

    public DetalleComprobacionPage(
        IServicioTranscript servicioTranscript,
        IServicioAlerta servicioAlerta,
        IServicioLogs logs)
    {
        InitializeComponent();
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _logs = logs;

        ActualizarEstadoCommand = new Command(async () => await ActualizarEstadoAsync());
        IrCapturaCommand = new Command(async () => await IrCapturaAsync());

        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("comprobacionId", out var idObj) && idObj is Guid id)
            _comprobacionId = id;

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
        if (_comprobacionId == Guid.Empty) return;
        if (_navegandoTrasEliminacion || _detalleNoDisponible) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.ObtenerComprobacionAsync(_comprobacionId);
            if (!res.Ok || res.Payload is null)
            {
                if (EsEntidadNoEncontrada(res.Error?.Mensaje))
                {
                    _detalleNoDisponible = true;
                    PaginaComprobaciones.PendienteActualizarListado = true;
                    await RegresarAListadoSiSigueAbiertaAsync();
                    return;
                }

                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo obtener la comprobación.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _comprobacion = res.Payload;
            ConfigurarEstadosDisponibles();
            await CargarCapturasRelacionadasAsync();
            RefrescarBindings();
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private void RefrescarBindings()
    {
        OnPropertyChanged(nameof(CreacionTexto));
        OnPropertyChanged(nameof(CierreTexto));
        OnPropertyChanged(nameof(RequeridoTexto));
        OnPropertyChanged(nameof(MontoTexto));
        OnPropertyChanged(nameof(MontoComprobadoTexto));
        OnPropertyChanged(nameof(ComprobadoPorcentajeTexto));
        OnPropertyChanged(nameof(DescripcionTexto));
        OnPropertyChanged(nameof(PuedeActualizarEstado));
    }

    private void ConfigurarEstadosDisponibles()
    {
        EstadosDisponibles.Clear();
        if (_comprobacion is null)
        {
            EstadoSeleccionado = null;
            return;
        }

        if (_comprobacion.Estado == EstadoComprobacion.Abierta)
        {
            EstadosDisponibles.Add(EstadoComprobacion.Cerrada.ToString());
            EstadosDisponibles.Add(EstadoComprobacion.Cancelada.ToString());
        }
        else if (_comprobacion.Estado is EstadoComprobacion.Cerrada or EstadoComprobacion.Cancelada)
        {
            EstadosDisponibles.Add(EstadoComprobacion.Abierta.ToString());
        }

        EstadoSeleccionado = EstadosDisponibles.FirstOrDefault();
    }

    private async Task CargarCapturasRelacionadasAsync()
    {
        if (_comprobacion is null)
        {
            CapturasRelacionadas = [];
            return;
        }

        try
        {
            var filtros = new List<Filtro>
            {
                new()
                {
                    Propiedad = "ProcesoAsociadoId",
                    Operador = Operador.Igual,
                    Valores = [_comprobacion.Id.ToString()]
                },
                new()
                {
                    Propiedad = "Tipo",
                    Operador = Operador.Igual,
                    Valores = [Contabee.Api.Transcript.TipoProcesoCaptura.Comprobacion.ToString()]
                }
            };

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

            var busqueda = new Busqueda
            {
                Filtros = filtros,
                OrdernarDesc = true,
                OrdenarPropiedad = "FechaCreacion",
                Paginado = new Paginado { Pagina = 1, TamanoPagina = 50 },
                Contar = true
            };

            var resultado = await _servicioTranscript.BusquedaCapturas(busqueda);
            CapturasRelacionadas = resultado.Elementos?
                .Select((e, i) => new FacturacionPage.ItemConConsecutivo(i + 1, e))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logs.Log($"[DetalleComprobacionPage] Error cargando capturas relacionadas: {ex.Message}");
            CapturasRelacionadas = [];
        }
    }

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (_comprobacion is null) return;

        ActualizarComprobacionPopup.ResultadoActualizar? result = null;
        await this.ShowPopupAsync(new ActualizarComprobacionPopup(_comprobacion, _rfc, r => result = r));
        if (result is null) return;

        EstaCargando = true;
        try
        {
            var req = new ActualizaComprobacion
            {
                Id = _comprobacion.Id,
                CuentaFiscalId = _comprobacion.CuentaFiscalId,
                UsuarioRceptorId = _comprobacion.UsuarioReceptorId,
                Monto = (double)result.Monto,
                PorcentajeCompropbar = result.PorcentajeComprobar,
                Cierre = result.Cierre,
                Descripcion = result.Descripcion
            };

            var res = await _servicioTranscript.ActualizarComprobacionAsync(_comprobacion.Id, req);
            if (!res.Ok || res.Payload is null)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo actualizar la comprobación.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _comprobacion = res.Payload;
            RefrescarBindings();
            PaginaComprobaciones.PendienteActualizarListado = true;
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async void OnEliminarClicked(object sender, EventArgs e)
    {
        if (_comprobacion is null) return;

        var confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar comprobación",
            "¿Deseas eliminar esta comprobación?",
            confirmarText: "Eliminar",
            cancelarText: "Cancelar");

        if (!confirmar) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.EliminarComprobacionAsync(_comprobacion.Id);
            if (!res.Ok)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo eliminar la comprobación.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            PaginaComprobaciones.PendienteActualizarListado = true;
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
        if (_comprobacion is null || string.IsNullOrWhiteSpace(EstadoSeleccionado)) return;
        if (!Enum.TryParse<EstadoComprobacion>(EstadoSeleccionado, out var estadoNuevo)) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.ActualizarEstadoComprobacionAsync(_comprobacion.Id, estadoNuevo);
            if (!res.Ok || res.Payload is null)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo actualizar el estado.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _comprobacion = res.Payload;
            ConfigurarEstadosDisponibles();
            RefrescarBindings();
            PaginaComprobaciones.PendienteActualizarListado = true;
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task IrCapturaAsync()
    {
        if (_comprobacion is null) return;

        FacturacionPage.ProcesoAsociadoFiltroId = _comprobacion.Id;
        FacturacionPage.ProcesoAsociadoFiltroTipo = Contabee.Api.Transcript.TipoProcesoCaptura.Comprobacion;
        await Shell.Current.GoToAsync(nameof(PaginaCaptura),
            new Dictionary<string, object>
            {
                ["tipo"] = Contabee.Api.Transcript.TipoProcesoCaptura.Comprobacion,
                ["procesoId"] = _comprobacion.Id
            });
    }
}
