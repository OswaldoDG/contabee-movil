using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
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
    public string EstadoTexto => _devolucion?.Estado switch
    {
        EstadoDevolucion.Admitida => "Abrir",
        _ => _devolucion?.Estado.ToString() ?? "-"
    };
    public string MontoTexto => (_devolucion?.Monto ?? 0d).ToString("C2");
    public string CreacionTexto => _devolucion?.Creacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "-";
    public string DescripcionTexto => string.IsNullOrWhiteSpace(_devolucion?.Descripcion) ? "Sin descripción" : _devolucion!.Descripcion!;

    public bool PuedeActualizarEstado => EsUsuarioPrimario && EstadosDisponibles.Count > 0;
    public bool EsUsuarioPrimario => AppState.Instance.CuentaFiscalActual?.TipoCuenta == Contabee.Api.Crm.TipoCuenta.Primaria;

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

                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo obtener la devolución.", verBotonCancelar: false, confirmarText: "OK");
                return;
            }

            _devolucion = res.Payload;
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

        if (_devolucion.Estado == EstadoDevolucion.Admitida)
        {
            EstadosDisponibles.Add("Aceptar");
            EstadosDisponibles.Add("Declinar");
        }
        else if (_devolucion.Estado is EstadoDevolucion.Aceptada or EstadoDevolucion.Declinada)
        {
            EstadosDisponibles.Add("Abrir");
        }

        EstadoSeleccionado = EstadosDisponibles.FirstOrDefault();
    }

    private async Task CargarCapturasRelacionadasAsync()
    {
        if (_devolucion is null)
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
                    Valores = [_devolucion.Id.ToString()]
                },
                new()
                {
                    Propiedad = "Tipo",
                    Operador = Operador.Igual,
                    Valores = [Contabee.Api.Transcript.TipoProcesoCaptura.Devolucion.ToString()]
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
            _logs.Log($"[DetalleDevolucionPage] Error cargando capturas relacionadas: {ex.Message}");
            CapturasRelacionadas = [];
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
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo actualizar la devolución.", verBotonCancelar: false, confirmarText: "OK");
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
            "Eliminar devolución",
            "¿Deseas eliminar esta devolución?",
            confirmarText: "Eliminar",
            cancelarText: "Cancelar");

        if (!confirmar) return;

        EstaCargando = true;
        try
        {
            var res = await _servicioTranscript.EliminarDevolucionAsync(_devolucion.Id);
            if (!res.Ok)
            {
                await _servicioAlerta.MostrarAsync("Error", res.Error?.Mensaje ?? "No se pudo eliminar la devolución.", verBotonCancelar: false, confirmarText: "OK");
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
        if (!Enum.TryParse<EstadoDevolucion>(EstadoSeleccionado, out var estadoNuevo)) return;

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
