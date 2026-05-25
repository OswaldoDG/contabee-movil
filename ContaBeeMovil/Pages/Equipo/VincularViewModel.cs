using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using ContaBeeMovil.Pages;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Equipo;

public class VincularViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly AppState _appState;

    private bool _esConCuenta;
    private string _titulo = string.Empty;
    private bool _mostrarFormulario;
    private bool _estaCargando;
    private bool _cancelado;

    private string _tokenIngresado = string.Empty;
    private IReadOnlyList<string> _tokenChars = ["", "", "", ""];
    private string _tokenValidado = string.Empty;

    private string _nombre = string.Empty;
    private string _email = string.Empty;
    private string _telefono = string.Empty;

    public bool EsConCuenta
    {
        get => _esConCuenta;
        set
        {
            _esConCuenta = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NoEsConCuenta));
            Titulo = value ? "Vincular con cuenta" : "Agregar sin cuenta";
        }
    }

    public bool NoEsConCuenta => !_esConCuenta;

    public string Titulo
    {
        get => _titulo;
        private set { _titulo = value; OnPropertyChanged(); }
    }

    public bool MostrarPasoUno => !_mostrarFormulario;

    public bool MostrarFormulario
    {
        get => _mostrarFormulario;
        private set
        {
            _mostrarFormulario = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MostrarPasoUno));
        }
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        private set
        {
            _estaCargando = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NoCargando));
            OnPropertyChanged(nameof(PuedeVincular));
        }
    }

    public bool NoCargando => !_estaCargando;

    public string TokenIngresado
    {
        get => _tokenIngresado;
        set
        {
            var upper = value.ToUpperInvariant();
            if (_tokenIngresado == upper) return;
            _tokenIngresado = upper;
            OnPropertyChanged();

            var chars = new List<string> { "", "", "", "" };
            for (int i = 0; i < Math.Min(upper.Length, 4); i++)
                chars[i] = upper[i].ToString();
            TokenChars = chars;

            if (upper.Length == 4 && !_estaCargando)
                _ = VincularPasoUnoAsync();
        }
    }

    public IReadOnlyList<string> TokenChars
    {
        get => _tokenChars;
        private set { _tokenChars = value; OnPropertyChanged(); }
    }

    public string Nombre
    {
        get => _nombre;
        set
        {
            _nombre = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PuedeVincular));
        }
    }

    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    public string Telefono
    {
        get => _telefono;
        set { _telefono = value; OnPropertyChanged(); }
    }

    public bool PuedeVincular => !_estaCargando && !string.IsNullOrWhiteSpace(_nombre);

    public event EventHandler? VinculacionSinCuentaExitosa;

    public ICommand VincularCommand { get; }
    public ICommand CancelarCargaCommand { get; }
    public ICommand CancelarFormularioCommand { get; }

    public VincularViewModel(
        IServicioIdentidad servicioIdentidad,
        IServicioSesion servicioSesion,
        IServicioToast toast,
        AppState appState)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion    = servicioSesion;
        _toast             = toast;
        _appState          = appState;

        VincularCommand          = new Command(async () => await VincularPasoDosAsync());
        CancelarCargaCommand     = new Command(CancelarCarga);
        CancelarFormularioCommand = new Command(CancelarFormulario);
    }

    private void CancelarCarga()
    {
        _cancelado = true;
        EstaCargando   = false;
        TokenIngresado = string.Empty;
    }

    private void CancelarFormulario()
    {
        _tokenValidado = string.Empty;
        Nombre         = string.Empty;
        Email          = string.Empty;
        Telefono       = string.Empty;
        TokenIngresado = string.Empty;
        MostrarFormulario = false;
    }

    private async Task VincularPasoUnoAsync()
    {
        _cancelado = false;
        EstaCargando   = true;
        try
        {
            var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId ?? Guid.Empty;
            var r = await _servicioIdentidad.VincularUsuario(cfid, new SolictudVinculacion
            {
                TokenVinculacion = _tokenIngresado,
                UsuarioExistente = _esConCuenta
            });

            if (_cancelado) return;

            if (!r.Ok)
            {
                var msg = r.HttpCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Token para tipo de vinculación incorrecto.",
                    System.Net.HttpStatusCode.NotFound   => "Código de vinculación no válido.",
                    _                         => "Ha ocurrido un error al vincular."
                };
                await _toast.MostrarAsync(msg, ToastIcono.Error);
                TokenIngresado = string.Empty;
                return;
            }

            if (_esConCuenta)
            {
                await _servicioSesion.GetMisUsuariosAsync();
                await _servicioSesion.GetAsociacionesFiscalesAsync();
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync("..");
                });
                _ = _toast.MostrarAsync("¡Vinculación completada!", ToastIcono.Info);
            }
            else
            {
                _tokenValidado = _tokenIngresado;
                MostrarFormulario = true;
            }
        }
        catch
        {
            if (!_cancelado)
            {
                await _toast.MostrarAsync("Error al vincular el usuario.", ToastIcono.Error);
                TokenIngresado = string.Empty;
            }
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task VincularPasoDosAsync()
    {
        if (string.IsNullOrWhiteSpace(_nombre)) return;

        EstaCargando = true;
        try
        {
            var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId ?? Guid.Empty;
            var r = await _servicioIdentidad.VincularUsuarioLoginLess(cfid, new SolictudTokenLoginless
            {
                TokenVinculacion = _tokenValidado,
                Nombre           = _nombre,
                Email            = string.IsNullOrWhiteSpace(_email)    ? null : _email,
                Telefono         = string.IsNullOrWhiteSpace(_telefono) ? null : _telefono
            });

            if (!r.Ok)
            {
                var msg = r.Error?.Mensaje ?? "Error al crear el colaborador.";
                await _toast.MostrarAsync(msg, ToastIcono.Error);
                return;
            }

            await _servicioSesion.GetMisUsuariosAsync();
            await _servicioSesion.GetAsociacionesFiscalesAsync();
            _ = _toast.MostrarAsync("¡Usuario vinculado correctamente!", ToastIcono.Info);
            VinculacionSinCuentaExitosa?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            await _toast.MostrarAsync("Error al vincular el usuario.", ToastIcono.Error);
        }
        finally
        {
            EstaCargando = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
