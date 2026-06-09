using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using ContaBeeMovil.Pages;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Maui.Devices;

namespace ContaBeeMovil.Pages.Equipo;

public class VincularViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly IServicioAlerta _alerta;
    private readonly AppState _appState;
    private readonly IServicioLogs _logs;

    private bool _esConCuenta;
    private string _titulo = string.Empty;
    private bool _mostrarFormulario;
    private bool _estaCargando;
    private bool _cancelado;

    private string _tokenIngresado = string.Empty;
    private IReadOnlyList<TokenCaracter> _tokenChars = ConstruirCaracteres4("");
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

            TokenChars = ConstruirCaracteres4(upper);

            if (upper.Length == 4 && !_estaCargando)
                _ = VincularPasoUnoAsync();
        }
    }

    public IReadOnlyList<TokenCaracter> TokenChars
    {
        get => _tokenChars;
        private set { _tokenChars = value; OnPropertyChanged(); }
    }

    private static IReadOnlyList<TokenCaracter> ConstruirCaracteres4(string upper)
    {
        try
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var screenWidth = info.Density > 0 ? info.Width / info.Density : 390.0;
            var available = screenWidth - 32.0 - (3 * 8.0); // 16dp c/lado + 3 gaps de 8dp
            var ancho = Math.Max(36, Math.Min(84, Math.Floor(available / 4)));
            var alto = Math.Round(ancho * 1.38);
            var fontSize = Math.Round(ancho * 0.52);
            return Enumerable.Range(0, 4)
                .Select(i => new TokenCaracter(i < upper.Length ? upper[i].ToString() : "", ancho, alto, fontSize))
                .ToList();
        }
        catch
        {
            return Enumerable.Range(0, 4)
                .Select(i => new TokenCaracter(i < upper.Length ? upper[i].ToString() : "", 48, 66, 30))
                .ToList();
        }
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
        IServicioAlerta alerta,
        AppState appState,
        IServicioLogs logs)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion    = servicioSesion;
        _toast             = toast;
        _alerta            = alerta;
        _appState          = appState;
        _logs              = logs;

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
        _logs.Info($"[Vincular] PasoUno — esConCuenta={_esConCuenta}");
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
                if (r.HttpCode == System.Net.HttpStatusCode.Conflict && !_esConCuenta)
                {
                    _logs.Warn("[Vincular] PasoUno — conflicto 409 (dispositivo del invitado ya registrado)");
                    await _alerta.MostrarAsync(
                        "Dispositivo del invitado con conflicto",
                        "El dispositivo del invitado ya está registrado en otra cuenta. Pídele que abra la app e intente generar un nuevo código — la app lo guiará automáticamente.",
                        verBotonCancelar: false,
                        confirmarText: "Entendido");
                    TokenIngresado = string.Empty;
                    return;
                }

                _logs.Warn($"[Vincular] PasoUno — error HTTP={r.HttpCode} código={r.Error?.Codigo}");
                var msg = r.HttpCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Token para tipo de vinculación incorrecto.",
                    System.Net.HttpStatusCode.NotFound   => "Código de vinculación no válido.",
                    _                                    => "Ha ocurrido un error al vincular."
                };
                await _toast.MostrarAsync(msg, ToastIcono.Error);
                TokenIngresado = string.Empty;
                return;
            }

            if (_esConCuenta)
            {
                _logs.Info("[Vincular] PasoUno exitoso — flujo ConCuenta completado");
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
                _logs.Info("[Vincular] PasoUno exitoso — mostrando formulario SinCuenta");
                _tokenValidado = _tokenIngresado;
                MostrarFormulario = true;
            }
        }
        catch (Exception ex)
        {
            _logs.Error($"[Vincular] PasoUno excepción: {ex.GetType().Name} - {ex.Message}");
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
        _logs.Info("[Vincular] PasoDos — enviando datos del colaborador sin cuenta");
        try
        {
            var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId ?? Guid.Empty;
            var solicitud = new SolictudTokenLoginless
            {
                TokenVinculacion = _tokenValidado,
                Nombre           = _nombre,
                Email            = string.IsNullOrWhiteSpace(_email)    ? null : _email,
                Telefono         = string.IsNullOrWhiteSpace(_telefono) ? null : _telefono
            };

            var r = await _servicioIdentidad.VincularUsuarioLoginLess(cfid, solicitud);

            if (!r.Ok)
            {
                if (r.HttpCode == System.Net.HttpStatusCode.Conflict)
                {
                    var confirmar = await _alerta.MostrarAsync(
                        "Dispositivo ya registrado",
                        "Este dispositivo ya está vinculado a otra cuenta. ¿Deseas liberar el dispositivo y continuar?",
                        confirmarText: "Liberar y vincular");

                    if (!confirmar) return;

                    var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
                    var rEliminar = await _servicioIdentidad.EliminarAsociacionesDispositivo(dispositivoId);
                    if (!rEliminar.Ok)
                    {
                        await _toast.MostrarAsync("No se pudo liberar el dispositivo.", ToastIcono.Error);
                        return;
                    }

                    r = await _servicioIdentidad.VincularUsuarioLoginLess(cfid, solicitud);
                    if (!r.Ok)
                    {
                        var msg = r.Error?.Mensaje ?? "Error al vincular el colaborador.";
                        await _toast.MostrarAsync(msg, ToastIcono.Error);
                        return;
                    }
                }
                else
                {
                    var msg = r.Error?.Mensaje ?? "Error al crear el colaborador.";
                    await _toast.MostrarAsync(msg, ToastIcono.Error);
                    return;
                }
            }

            _logs.Info("[Vincular] PasoDos exitoso — colaborador vinculado");
            await _servicioSesion.GetMisUsuariosAsync();
            await _servicioSesion.GetAsociacionesFiscalesAsync();
            _ = _toast.MostrarAsync("¡Usuario vinculado correctamente!", ToastIcono.Info);
            VinculacionSinCuentaExitosa?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logs.Error($"[Vincular] PasoDos excepción: {ex.GetType().Name} - {ex.Message}");
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
