using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Views;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

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
    public double SwipeItemWidth { get; set; }

    public EquipoUsuarioItem(CuentaUsuario u)
    {
        Id = u.Id;
        Nombre = u.Nombre ?? u.UserName ?? u.Email ?? "Sin nombre";
        Email  = u.Email ?? "";

        var partes = Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Iniciales = partes.Length >= 2
            ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
            : Nombre.Length >= 2 ? Nombre[..2].ToUpper() : Nombre.ToUpper();

        TieneEmail = !string.IsNullOrWhiteSpace(Email);

        TipoCuentaTexto = u.TipoCuenta switch
        {
            TipoCuenta.Cliente          => "Propietario",
            TipoCuenta.Empleado         => "Empleado",
            TipoCuenta.EmpleadoCliente  => "Empleado / Cliente",
            TipoCuenta.UsuarioCaptura   => "Captura",
            TipoCuenta.LoginLessCliente => "Sin contraseña",
            _                           => "Colaborador"
        };
    }
}

public class EquipoViewModel : INotifyPropertyChanged
{
    private readonly AppState _appState;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioToast _toast;

    private ObservableCollection<EquipoUsuarioItem> _usuarios = [];
    private bool _estaCargando;
    private bool _estaRefrescando;
    private bool _sinUsuarios;
    private bool _tieneDatos;
    private bool _fabExpandido;
    private string? _miEmail;

    public ICommand PullRefreshCommand { get; }
    public ICommand ToggleFabCommand { get; }
    public ICommand AgregarCapturistaCommand { get; }
    public ICommand AgregarSinCuentaCommand { get; }
    public ICommand AgregarConCuentaCommand { get; }

    public EquipoViewModel(AppState appState, IServicioSesion servicioSesion, IServicioAlerta servicioAlerta, IServicioIdentidad servicioIdentidad, IServicioToast toast)
    {
        _appState           = appState;
        _servicioSesion     = servicioSesion;
        _servicioAlerta     = servicioAlerta;
        _servicioIdentidad  = servicioIdentidad;
        _toast              = toast;

        PullRefreshCommand       = new Command(async () => await PullRefreshAsync());
        ToggleFabCommand         = new Command(() => FabExpandido = !FabExpandido);
        AgregarCapturistaCommand = new Command(async () => await AgregarCapturistaAsync());
        AgregarSinCuentaCommand  = new Command(async () => await AgregarAsync(esConCuenta: false));
        AgregarConCuentaCommand  = new Command(async () => await AgregarAsync(esConCuenta: true));

        _appState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.MisUsuarios) or nameof(AppState.CuentaFiscalActual))
                ActualizarLista();
        };
    }

    public ObservableCollection<EquipoUsuarioItem> Usuarios
    {
        get => _usuarios;
        private set { _usuarios = value; OnPropertyChanged(); }
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        set { _estaCargando = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarVacio)); }
    }

    public bool EstaRefrescando
    {
        get => _estaRefrescando;
        set { _estaRefrescando = value; OnPropertyChanged(); }
    }

    public bool SinUsuarios
    {
        get => _sinUsuarios;
        set { _sinUsuarios = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarVacio)); }
    }

    public bool MostrarVacio => !_estaCargando && _sinUsuarios;

    public string PerfilInfo
    {
        get
        {
            var p = _appState.Perfil;
            return $"[Sesion] Email: {_miEmail ?? "(null)"}\n" +
                   $"[Sesion] EsLoginLess: {_appState.EsLoginLess}\n" +
                   $"[Perfil] DisplayName: {p?.DisplayName}\n" +
                   $"[Perfil] Iniciales: {p?.Iniciales}\n" +
                   $"[Perfil] CuentaFiscalId: {p?.CuentaFiscalId}";
        }
    }

    public bool TieneDatos
    {
        get => _tieneDatos;
        set { _tieneDatos = value; OnPropertyChanged(); }
    }

    public bool FabExpandido
    {
        get => _fabExpandido;
        set { _fabExpandido = value; OnPropertyChanged(); OnPropertyChanged(nameof(FabColapsado)); }
    }

    public bool FabColapsado => !_fabExpandido;

    public async Task CargarAsync(bool forzar = false)
    {
        if (_miEmail == null && !_appState.EsLoginLess)
        {
            _miEmail = await _servicioSesion.LeeEmailAsync();
            OnPropertyChanged(nameof(PerfilInfo));
        }

        if (!forzar && _appState.MisUsuarios is { Count: > 0 })
        {
            ActualizarLista();
            return;
        }

        EstaCargando = true;
        try
        {
            await _servicioSesion.GetMisUsuariosAsync();
            ActualizarLista();
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task AgregarAsync(bool esConCuenta)
    {
        FabExpandido = false;
        await Shell.Current.GoToAsync($"{nameof(VincularPage)}?esConCuenta={esConCuenta}");
    }

    private async Task AgregarCapturistaAsync()
    {
        FabExpandido = false;

        var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid == null) return;

        bool creado = false;
        var popup = new CrearCapturistaPopup(_servicioIdentidad, cfid.Value, r => creado = r);
        await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);

        if (creado)
            await CargarAsync(forzar: true);
    }

    private async Task PullRefreshAsync()
    {
        EstaRefrescando = true;
        try
        {
            await _servicioSesion.GetMisUsuariosAsync();
            ActualizarLista();
        }
        finally
        {
            EstaRefrescando = false;
        }
    }

    private void ActualizarLista()
    {
        var info       = DeviceDisplay.MainDisplayInfo;
        double density = info.Density > 0 ? info.Density : 1;
        double cardWidth   = (info.Width / density) - 32; // SwipeView Margin="16,6" → 16×2 horizontal
        double swipeWidth  = cardWidth * 0.75;

        var items = (_appState.MisUsuarios ?? [])
            .Where(u => _miEmail == null || !string.Equals(u.Email, _miEmail, StringComparison.OrdinalIgnoreCase))
            .Select(u =>
            {
                var item = new EquipoUsuarioItem(u);
                item.SwipeItemWidth  = swipeWidth;
                item.DeleteCommand   = new Command(async () => await ConfirmarEliminarAsync(item));
                return item;
            })
            .ToList();
        Usuarios    = new ObservableCollection<EquipoUsuarioItem>(items);
        SinUsuarios = Usuarios.Count == 0;
        TieneDatos  = Usuarios.Count > 0;
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
            await _toast.MostrarAsync("Usuario eliminado del equipo", ToastIcono.Info);
            await CargarAsync(forzar: true);
        }
        else
        {
            await _toast.MostrarAsync(resp.Error?.Mensaje ?? "Error al eliminar el usuario", ToastIcono.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
