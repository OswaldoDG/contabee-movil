using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using ContaBeeMovil.Config;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Views;
using Busqueda = Contabee.Api.Identidad.Busqueda;
using Paginado = Contabee.Api.Identidad.Paginado;
using CuentaUsuario = Contabee.Api.Identidad.CuentaUsuario;
using TipoCuentaUsuario = Contabee.Api.Identidad.TipoCuentaUsuario;

namespace ContaBeeMovil.Pages.Equipo;

public class EquipoUsuarioItem
{
    public Guid Id { get; }
    public string Nombre { get; }
    public string Email { get; }
    public string Iniciales { get; }
    public bool TieneEmail { get; }
    public string TipoCuentaTexto { get; }
    public ICommand? DeleteCommand { get; set; }
    public ICommand? ConfigurarCommand { get; set; }
    public bool MostrarConfigurar { get; set; }
    public double SwipeItemWidth { get; set; }
    public bool EsUsuarioPropio { get; set; }
    public bool AsociacionActiva { get; set; }

    public EquipoUsuarioItem(CuentaUsuario u)
    {
        Id = u.Id;
        Nombre = u.Nombre ?? u.UserName ?? u.Email ?? "Sin nombre";
        Email  = u.Email ?? "";
        AsociacionActiva = u.AsociacionActiva;

        var partes = Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Iniciales = partes.Length >= 2
            ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
            : Nombre.Length >= 2 ? Nombre[..2].ToUpper() : Nombre.ToUpper();

        TieneEmail = !string.IsNullOrWhiteSpace(Email);

        TipoCuentaTexto = u.TipoCuenta switch
        {
            TipoCuentaUsuario.Cliente          => "Propietario",
            TipoCuentaUsuario.Empleado         => "Empleado",
            TipoCuentaUsuario.EmpleadoCliente  => "Empleado / Cliente",
            TipoCuentaUsuario.LoginLessCliente => "Sin contraseña",
            TipoCuentaUsuario.UsuarioCaptura   => "Captura",
            _                                  => "Colaborador"
        };
    }
}

public class EquipoViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioToast _toast;
    private readonly IServicioLogs _logs;

    private Busqueda? _ultimaBusqueda;
    private int _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
    private string? _miEmail;

    private IEnumerable<EquipoUsuarioItem>? _elementos;
    private bool _estaCargando;
    private long _totalEncontrados;
    private int _paginaActual = 1;
    private int _totalPaginas = 1;
    private bool _consultaEjecutada;
    private bool _fabExpandido;

    public ICommand BuscarEquipoCommand { get; }
    public ICommand PaginaAnteriorCommand { get; }
    public ICommand PaginaSiguienteCommand { get; }
    public ICommand ToggleFabCommand { get; }
    public ICommand AgregarSinCuentaCommand { get; }
    public ICommand AgregarConCuentaCommand { get; }

    public EquipoViewModel(AppState appState, IServicioSesion servicioSesion, IServicioAlerta servicioAlerta, IServicioIdentidad servicioIdentidad, IServicioCrm servicioCrm, IServicioToast toast, IServicioLogs logs)
    {
        _appState           = appState;
        _servicioSesion     = servicioSesion;
        _servicioAlerta     = servicioAlerta;
        _servicioIdentidad  = servicioIdentidad;
        _servicioCrm        = servicioCrm;
        _toast              = toast;
        _logs               = logs;

        BuscarEquipoCommand      = new Command<Busqueda>(async b => await OnBuscarEquipo(b));
        PaginaAnteriorCommand    = new Command(async () => await EjecutarBusqueda(PaginaActual - 1));
        PaginaSiguienteCommand   = new Command(async () => await EjecutarBusqueda(PaginaActual + 1));
        ToggleFabCommand         = new Command(() => FabExpandido = !FabExpandido);
        AgregarSinCuentaCommand  = new Command(async () => await AgregarAsync(esConCuenta: false));
        AgregarConCuentaCommand  = new Command(async () => await AgregarAsync(esConCuenta: true));

        _appState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.CuentaFiscalActual))
            {
                if (_ultimaBusqueda is not null)
                    _ = EjecutarBusqueda(1);
            }
            else if (e.PropertyName == nameof(AppState.EsDev))
                OnPropertyChanged(nameof(EsDev));
        };
    }

    public IEnumerable<EquipoUsuarioItem>? Elementos
    {
        get => _elementos;
        private set { _elementos = value; OnPropertyChanged(); }
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        private set { _estaCargando = value; OnPropertyChanged(); }
    }

    public long TotalEncontrados
    {
        get => _totalEncontrados;
        private set { _totalEncontrados = value; OnPropertyChanged(); }
    }

    public int PaginaActual
    {
        get => _paginaActual;
        private set { _paginaActual = value; OnPropertyChanged(); }
    }

    public int TotalPaginas
    {
        get => _totalPaginas;
        private set { _totalPaginas = value; OnPropertyChanged(); }
    }

    public bool ConsultaEjecutada
    {
        get => _consultaEjecutada;
        private set { _consultaEjecutada = value; OnPropertyChanged(); }
    }

    public bool EsDev => _appState.EsDev;

    public bool FabExpandido
    {
        get => _fabExpandido;
        set { _fabExpandido = value; OnPropertyChanged(); OnPropertyChanged(nameof(FabColapsado)); }
    }

    public bool FabColapsado => !_fabExpandido;

    private async Task OnBuscarEquipo(Busqueda busqueda)
    {
        if (_appState.ModoOffline) return;

        if (_appState.CuentaFiscalActual is null)
        {
            await _toast.MostrarAsync("Selecciona una cuenta fiscal", ToastIcono.Warning);
            return;
        }

        if (_miEmail == null && !_appState.EsLoginLess)
            _miEmail = await _servicioSesion.LeeEmailAsync();

        _ultimaBusqueda = busqueda;
        _tamanoPaginaEfectivo = AppSettings.Consulta.TamanoPagina;
        await EjecutarBusqueda(1);
    }

    private async Task EjecutarBusqueda(int pagina)
    {
        if (_ultimaBusqueda is null) return;

        var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid is null) return;

        if (_appState.MisUsuarios is null || _appState.MisUsuarios.Count == 0)
            await _servicioSesion.GetMisUsuariosAsync();

        // El endpoint de identidad usa paginación 0-based; la UI es 1-based.
        _ultimaBusqueda.Paginado = new Paginado
        {
            Pagina = pagina - 1,
            TamanoPagina = AppSettings.Consulta.TamanoPagina
        };

        var filtrosTxt = _ultimaBusqueda.Filtros is null
            ? "(sin filtros)"
            : string.Join(" | ", _ultimaBusqueda.Filtros.Select(f =>
                $"{f.Propiedad}:{f.Operador}=[{string.Join(",", f.Valores ?? [])}]"));

        EstaCargando = true;
        try
        {
            _logs.Log($"[Equipo] Ejecutando búsqueda. Pagina={pagina}, Orden={_ultimaBusqueda.OrdenarPropiedad}, Desc={_ultimaBusqueda.OrdernarDesc}, Payload={filtrosTxt}");
            var resp = await _servicioIdentidad.BuscarUsuarios(cfid.Value, _ultimaBusqueda);

            if (!resp.Ok || resp.Payload is null)
            {
                _logs.Warn($"[Equipo] Error en búsqueda — {resp.Error?.Codigo}: {resp.Error?.Mensaje}");
                await _servicioAlerta.MostrarAsync(
                    "Error",
                    "No se pudieron cargar los usuarios. Intenta de nuevo.",
                    verBotonCancelar: false,
                    confirmarText: "OK");
                return;
            }

            var elementosPagina = resp.Payload.Elementos?.ToList() ?? [];
            if (pagina == 1 && elementosPagina.Count > 0)
                _tamanoPaginaEfectivo = elementosPagina.Count;

            Elementos = MapearItems(elementosPagina);

            TotalEncontrados = resp.Payload.Total;
            PaginaActual = pagina;
            var divisorPaginas = Math.Max(1, _tamanoPaginaEfectivo);
            TotalPaginas = (int)Math.Ceiling((double)resp.Payload.Total / divisorPaginas);
            if (TotalPaginas < 1) TotalPaginas = 1;
            ConsultaEjecutada = true;
        }
        catch (Exception ex)
        {
            _logs.Error($"[Equipo] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync(
                "Error",
                "No se pudieron cargar los usuarios. Intenta de nuevo.",
                verBotonCancelar: false,
                confirmarText: "OK");
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private List<EquipoUsuarioItem> MapearItems(IEnumerable<CuentaUsuario> usuarios)
    {
        var info       = DeviceDisplay.MainDisplayInfo;
        double density = info.Density > 0 ? info.Density : 1;
        double cardWidth   = (info.Width / density) - 32; // SwipeView Margin="16,6" → 16×2 horizontal
        double swipeWidth  = cardWidth * 0.75;

        bool esPrimario = _appState.CuentaFiscalActual?.TipoCuenta == TipoCuenta.Primaria;

        return usuarios
            .Select(u =>
            {
                bool esPropio = _miEmail != null &&
                                string.Equals(u.Email, _miEmail, StringComparison.OrdinalIgnoreCase);
                var item = new EquipoUsuarioItem(u)
                {
                    SwipeItemWidth    = swipeWidth,
                    MostrarConfigurar = esPrimario,
                    EsUsuarioPropio   = esPropio
                };
                if (!esPropio)
                    item.DeleteCommand = new Command(async () => await ConfirmarEliminarAsync(item));
                if (esPrimario)
                    item.ConfigurarCommand = new Command(async () => await AbrirPropiedadesAsync(item));
                return item;
            })
            .ToList();
    }

    private async Task AgregarAsync(bool esConCuenta)
    {
        FabExpandido = false;
        await Shell.Current.GoToAsync($"{nameof(VincularPage)}?esConCuenta={esConCuenta}");
    }

    private async Task AbrirPropiedadesAsync(EquipoUsuarioItem item)
    {
        var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid == null) return;
        var popup = new PropiedadesUsuarioPopup(_servicioCrm, _toast, _appState, cfid.Value, item.Id, item.Nombre, item.AsociacionActiva, item.EsUsuarioPropio,
            onAsociacionCambiada: async () =>
            {
                await _servicioSesion.GetMisUsuariosAsync();
                await EjecutarBusqueda(PaginaActual);
            });
        await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);
    }

    private async Task ConfirmarEliminarAsync(EquipoUsuarioItem item)
    {
        bool confirmar = await _servicioAlerta.MostrarAsync(
            "Eliminar usuario",
            $"¿Desea eliminar a {item.Nombre} del equipo?",
            confirmarText: "Eliminar", cancelarText: "Cancelar");

        if (!confirmar) return;

        var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid == null) return;

        var resp = await _servicioIdentidad.EliminarVinculoUsuario(cfid.Value, item.Id);

        if (resp.Ok)
        {
            _logs.Info($"[Equipo] Usuario eliminado — Id={item.Id}");
            _ = _toast.MostrarAsync("Usuario eliminado del equipo", ToastIcono.Info);
            await _servicioSesion.GetMisUsuariosAsync();
            await EjecutarBusqueda(PaginaActual);
        }
        else
        {
            _logs.Warn($"[Equipo] Error eliminando usuario — {resp.Error?.Codigo}: {resp.Error?.Mensaje}");
            await _toast.MostrarAsync(resp.Error?.Mensaje ?? "Error al eliminar el usuario", ToastIcono.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
