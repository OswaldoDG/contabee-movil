using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using ContaBeeMovil.Config;
using ContaBeeMovil.Pages.Comprobaciones;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Pages.Comprobaciones;

public partial class PaginaComprobaciones : ContentPage
{
    internal static bool PendienteActualizarListado { get; set; }

    private readonly IServicioTranscript _servicioTranscript;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioLogs _logs;
    private Busqueda? _ultimaBusqueda;
    private int _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
    private bool _abriendoPopup;

    private static readonly PopupOptions _popupOpts = new()
    {
        PageOverlayColor = Color.FromArgb("#66000000"),
        CanBeDismissedByTappingOutsideOfPopup = false,
    };

    private bool _estaCargando;
    public bool EstaCargando
    {
        get => _estaCargando;
        private set { _estaCargando = value; OnPropertyChanged(); }
    }

    private bool _mostrarOverlayCarga;
    public bool MostrarOverlayCarga
    {
        get => _mostrarOverlayCarga;
        private set { _mostrarOverlayCarga = value; OnPropertyChanged(); }
    }

    private string _textoOverlayCarga = "Cargando comprobaciones...";
    public string TextoOverlayCarga
    {
        get => _textoOverlayCarga;
        private set { _textoOverlayCarga = value; OnPropertyChanged(); }
    }

    private IEnumerable<ItemConConsecutivo>? _elementos;
    public IEnumerable<ItemConConsecutivo>? Elementos
    {
        get => _elementos;
        private set { _elementos = value; OnPropertyChanged(); }
    }

    public record ItemConConsecutivo(int Consecutivo, Comprobacion Datos, string CuentaFiscalRfc);

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

    public ICommand BuscarComprobacionesCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }
    public ICommand AbrirDetalleCommand { get; }

    public FiltrosComprobacionesView Filtros => PanelFiltros;

    public PaginaComprobaciones(
        IServicioTranscript servicioTranscript,
        IServicioAlerta servicioAlerta,
        IServicioSesion servicioSesion,
        IServicioLogs logs)
    {
        _servicioTranscript = servicioTranscript;
        _servicioAlerta = servicioAlerta;
        _servicioSesion = servicioSesion;
        _logs = logs;

        BuscarComprobacionesCommand = new Command<Busqueda>(async b => await OnBuscarComprobaciones(b));
        PaginaAnteriorCommand = new Command(async () => await EjecutarBusqueda(PaginaActual - 1));
        PaginaSiguienteCommand = new Command(async () => await EjecutarBusqueda(PaginaActual + 1));
        AbrirDetalleCommand = new Command<ItemConConsecutivo>(async item => await AbrirDetalleAsync(item));

        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!ConsultaEjecutada)
        {
            _ = OnBuscarComprobaciones(PanelFiltros.BusquedaActual);
            return;
        }

        if (PendienteActualizarListado)
        {
            PendienteActualizarListado = false;
            _ = RecargarConUltimosFiltrosAsync();
        }
    }

    private async Task AbrirDetalleAsync(ItemConConsecutivo? item)
    {
        if (item is null) return;

        await Shell.Current.GoToAsync(nameof(DetalleComprobacionPage),
            new Dictionary<string, object>
            {
                ["comprobacionId"] = item.Datos.Id,
                ["rfc"] = item.CuentaFiscalRfc
            });
    }

    public void OnTabActivated()
    {
        if (!PendienteActualizarListado) return;

        PendienteActualizarListado = false;
        _ = RecargarConUltimosFiltrosAsync();
    }

    private async Task RecargarConUltimosFiltrosAsync()
    {
        if (_ultimaBusqueda is null)
        {
            await OnBuscarComprobaciones(PanelFiltros.BusquedaActual);
            return;
        }

        await EjecutarBusqueda(Math.Max(1, PaginaActual));
    }

    private async Task OnBuscarComprobaciones(Busqueda busqueda)
    {
        if (AppState.Instance.CuentaFiscalActual is null)
        {
            await this.ShowPopupAsync(new CuentaFiscalSelectorPopup());
            return;
        }

        _ultimaBusqueda = busqueda;
        _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
        await EjecutarBusqueda(1);
    }

    private async Task EjecutarBusqueda(int pagina)
    {
        if (_ultimaBusqueda is null) return;

        _ultimaBusqueda.Paginado = new Paginado
        {
            Pagina = pagina,
            TamanoPagina = AppSettings.Consulta.TamanoPagina
        };

        var filtrosTxt = _ultimaBusqueda.Filtros is null
            ? "(sin filtros)"
            : string.Join(" | ", _ultimaBusqueda.Filtros.Select(f =>
                $"{f.Propiedad}:{f.Operador}=[{string.Join(",", f.Valores ?? [])}]"));

        EstaCargando = true;
        try
        {
            _logs.Log($"[PaginaComprobaciones] Ejecutando búsqueda. Pagina={pagina}, Filtros={_ultimaBusqueda.Filtros?.Count ?? 0}, Orden={_ultimaBusqueda.OrdenarPropiedad}, Desc={_ultimaBusqueda.OrdernarDesc}, Payload={filtrosTxt}");
            var resultado = await _servicioTranscript.BusquedaComprobaciones(_ultimaBusqueda);

            var elementosPagina = resultado.Elementos?.ToList() ?? [];
            if (pagina == 1 && elementosPagina.Count > 0)
                _tamanoPaginaEfectivo = elementosPagina.Count;

            var offset = (pagina - 1) * _tamanoPaginaEfectivo;
            Elementos = elementosPagina
                .Select((e, i) => new ItemConConsecutivo(offset + i + 1, e, ResolverRfcCuentaFiscal(e)))
                .ToList();

            TotalEncontrados = resultado.Total;
            PaginaActual = pagina;
            var divisorPaginas = Math.Max(1, _tamanoPaginaEfectivo);
            TotalPaginas = (int)Math.Ceiling((double)resultado.Total / divisorPaginas);
            if (TotalPaginas < 1) TotalPaginas = 1;
            ConsultaEjecutada = true;
        }
        catch (ApiException ex)
        {
            _logs.Log($"[PaginaComprobaciones] ApiException Status={ex.StatusCode} Body={ex.Response}");
            await _servicioAlerta.MostrarAsync(
                "Error",
                !string.IsNullOrWhiteSpace(ex.Response)
                    ? ex.Response
                    : "No se pudieron cargar las comprobaciones. Intenta de nuevo.",
                verBotonCancelar: false,
                confirmarText: "OK");
        }
        catch (Exception ex)
        {
            _logs.Log($"[PaginaComprobaciones] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync(
                "Error",
                "No se pudieron cargar las comprobaciones. Intenta de nuevo.",
                verBotonCancelar: false,
                confirmarText: "OK");
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async void OnCrearComprobacionClicked(object sender, TappedEventArgs e)
    {
        if (_abriendoPopup) return;
        _abriendoPopup = true;

        try
        {
            var hostPage = ObtenerPaginaActivaParaPopup() ?? this;
            _logs.Log($"[PaginaComprobaciones-Crear] HostPage={hostPage.GetType().Name}");

            if (AppState.Instance.CuentaFiscalActual is null)
            {
                await hostPage.ShowPopupAsync(new CuentaFiscalSelectorPopup(), _popupOpts, CancellationToken.None);
                return;
            }

            if (AppState.Instance.MisUsuarios is null || AppState.Instance.MisUsuarios.Count == 0)
                await _servicioSesion.GetMisUsuariosAsync();

            var destinatarios = ObtenerDestinatarios()
                .Where(d => d.UsuarioId != Guid.Empty)
                .ToList();

            if (destinatarios.Count == 0)
            {
                await _servicioAlerta.MostrarAsync(
                    "Usuarios",
                    "Aún no se cargan los usuarios destinatarios. Verifica tu conexión e intenta de nuevo.",
                    verBotonCancelar: false,
                    confirmarText: "Aceptar");
                return;
            }

            CrearComprobacionPopup.ResultadoCrearComprobacion? captura = null;
            await hostPage.ShowPopupAsync(
                new CrearComprobacionPopup(destinatarios, result => captura = result),
                _popupOpts,
                CancellationToken.None);

            if (captura is null) return;

            var request = new CreaComprobacion
            {
                CuentaFiscalId = AppState.Instance.CuentaFiscalActual.CuentaFiscalId,
                UsuarioReceptorId = captura.UsuarioReceptorId,
                Monto = (double)captura.Monto,
                PorcentajeCompropbar = captura.PorcentajeComprobar,
                Cierre = captura.Cierre,
                Descripcion = captura.Descripcion
            };

            _logs.Log($"[PaginaComprobaciones-Crear] Request CuentaFiscalId={request.CuentaFiscalId}, UsuarioReceptorId={request.UsuarioReceptorId}, Monto={request.Monto}, Porcentaje={request.PorcentajeCompropbar}, Cierre={request.Cierre:O}, Descripcion='{request.Descripcion}'");

            TextoOverlayCarga = "Creando comprobación...";
            MostrarOverlayCarga = true;

            var respuesta = await _servicioTranscript.CrearComprobacionAsync(request);
            if (!respuesta.Ok)
            {
                MostrarOverlayCarga = false;
                await _servicioAlerta.MostrarAsync(
                    "Error",
                    respuesta.Error?.Mensaje ?? "No se pudo crear la comprobación.",
                    verBotonCancelar: false,
                    confirmarText: "OK");
                return;
            }

            PanelFiltros.SeleccionarPeriodoActual();
            _ultimaBusqueda = PanelFiltros.BusquedaActual;

            MostrarOverlayCarga = false;
            await EjecutarBusqueda(1);
        }
        catch (Exception ex)
        {
            _logs.Log($"[PaginaComprobaciones-Crear] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync(
                "Error",
                "No se pudo abrir o crear la comprobación.",
                verBotonCancelar: false,
                confirmarText: "OK");
        }
        finally
        {
            MostrarOverlayCarga = false;
            _abriendoPopup = false;
        }
    }

    private static Page? ObtenerPaginaActivaParaPopup()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is Shell shell)
            page = shell.CurrentPage;

        if (page is NavigationPage navigationPage)
            page = navigationPage.CurrentPage;

        if (page is TabbedPage tabbedPage)
            page = tabbedPage.CurrentPage;

        return page;
    }

    private static string ResolverRfcCuentaFiscal(Comprobacion comprobacion)
    {
        var cuentaFiscalId = comprobacion.CuentaFiscalId.ToString();
        if (string.IsNullOrWhiteSpace(cuentaFiscalId))
            return "RFC no disponible";

        var cuenta = AppState.Instance.CuentasFiscales?
            .FirstOrDefault(c => c.CuentaFiscalId.ToString().Equals(cuentaFiscalId, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(cuenta?.Rfc) ? "RFC no disponible" : cuenta.Rfc;
    }

    private static List<(Guid UsuarioId, string Nombre)> ObtenerDestinatarios()
    {
        var usuarios = (AppState.Instance.MisUsuarios ?? [])
            .Where(u => !u.EsCaptura && u.Estado == Contabee.Api.Identidad.EstadoCuenta.Activo)
            .ToList();
        var cuentaFiscalActualId = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;

        var usuarioSesion = usuarios
            .FirstOrDefault(u => !u.EsCaptura && u.CuentaFiscalId == cuentaFiscalActualId)
            ?? usuarios.FirstOrDefault(u => !u.EsCaptura)
            ?? usuarios.FirstOrDefault();

        var destinatarios = new List<(Guid UsuarioId, string Nombre)>();

        if (usuarioSesion is not null)
        {
            var nombreSesion = usuarioSesion.Nombre ?? usuarioSesion.UserName ?? usuarioSesion.Email ?? usuarioSesion.Id.ToString();
            if (nombreSesion.Contains('@'))
                nombreSesion = nombreSesion[..nombreSesion.IndexOf('@')];

            destinatarios.Add((usuarioSesion.Id, $"{nombreSesion} (Yo)"));
        }

        var secundarios = usuarios
            .Where(u => EsCuentaSecundaria(u.TipoCuenta)
                && u.CuentaFiscalId.HasValue
                && u.CuentaFiscalId.Value != cuentaFiscalActualId)
            .Select(u => new
            {
                Usuario = u,
                CuentaFiscalId = u.CuentaFiscalId!.Value
            })
            .GroupBy(x => x.CuentaFiscalId)
            .Select(g => g.First().Usuario)
            .ToList();

        destinatarios.AddRange(secundarios
            .Where(u => u.Id != usuarioSesion?.Id)
            .Select(u =>
            {
                var nombre = u.Nombre ?? u.UserName ?? u.Email ?? u.Id.ToString();
                if (nombre.Contains('@'))
                    nombre = nombre[..nombre.IndexOf('@')];
                return (u.Id, Nombre: $"{nombre} (Secundaria)");
            })
            .DistinctBy(u => u.Id));

        destinatarios.AddRange(usuarios
            .Where(u => !u.EsCaptura && u.Id != usuarioSesion?.Id)
            .Select(u =>
            {
                var nombre = u.Nombre ?? u.UserName ?? u.Email ?? u.Id.ToString();
                if (nombre.Contains('@'))
                    nombre = nombre[..nombre.IndexOf('@')];
                return (u.Id, Nombre: nombre);
            })
            .DistinctBy(u => u.Id));

        if (destinatarios.Count == 0)
        {
            destinatarios.AddRange(usuarios
                .Select(u =>
                {
                    var nombre = u.Nombre ?? u.UserName ?? u.Email ?? u.Id.ToString();
                    if (nombre.Contains('@'))
                        nombre = nombre[..nombre.IndexOf('@')];
                    return (u.Id, Nombre: nombre);
                })
                .DistinctBy(u => u.Id));
        }

        var resultado = destinatarios.DistinctBy(u => u.UsuarioId).ToList();

        return resultado;
    }

    private static bool EsCuentaSecundaria(Contabee.Api.Identidad.TipoCuenta tipoCuenta)
        => tipoCuenta is Contabee.Api.Identidad.TipoCuenta.Empleado
            or Contabee.Api.Identidad.TipoCuenta.EmpleadoCliente
            or Contabee.Api.Identidad.TipoCuenta.UsuarioCaptura;
}
