using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Ecommerce;
using Contabee.Api.Identidad;
using ContaBeeMovil;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Views;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Contabee.Pages.Registro;

public class RegistroViewModel : INotifyPropertyChanged
{
    private string _nombre;
    private string _email;
    private string _password;
    private string _confirmarPassword;
    private string _cuponRegistro;
    private string _mensajeError;
    private bool _estaCargando;
    private bool _aceptaPrivacidad;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioEcommerce _servicioEcommerce;
    private readonly IServicioToast _toast;
    private readonly DeviceService _deviceService;
    private readonly IServicioLogs _logs;
    private readonly IAppLogger _logger;

    public RegistroViewModel(IServicioIdentidad servicioIdentidad, IServicioEcommerce servicioEcommerce, IServicioToast servicioToast, DeviceService deviceService, IServicioLogs logs, IAppLogger logger)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioEcommerce = servicioEcommerce;
        _toast = servicioToast;
        _deviceService = deviceService;
        _logs = logs;
        _logger = logger;
        RegistrarCommand = new Command(async () => await Registrar(), () => PuedeRegistrar);
        IrALoginCommand = new Command(async () => await IrALogin());
    }
    public string Nombre
    {
        get => _nombre;
        set
        {
            _nombre = value;
            OnPropertyChanged();
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public string ConfirmarPassword
    {
        get => _confirmarPassword;
        set
        {
            _confirmarPassword = value;
            OnPropertyChanged();
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public string CuponRegistro
    {
        get => _cuponRegistro;
        set
        {
            _cuponRegistro = value;
            OnPropertyChanged();
        }
    }

    public string MensajeError
    {
        get => _mensajeError;
        set
        {
            _mensajeError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TieneError));
        }
    }

    public bool TieneError => !string.IsNullOrEmpty(MensajeError);

    public bool EstaCargando
    {
        get => _estaCargando;
        set
        {
            _estaCargando = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PuedeRegistrar));
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public bool AceptaPrivacidad
    {
        get => _aceptaPrivacidad;
        set
        {
            _aceptaPrivacidad = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PuedeRegistrar));
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public bool PuedeRegistrar =>
        !EstaCargando &&
        AceptaPrivacidad &&
        !string.IsNullOrWhiteSpace(Nombre) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        Password.Length >= 6 &&
        Password == ConfirmarPassword;

    public ICommand RegistrarCommand { get; }
    public ICommand IrALoginCommand { get; }
    public bool EsMinimo6 { get;  set; }
    public bool TieneMayuscula { get;  set; }
    public bool TieneNumero { get;  set; }
    public bool TieneCaracterEspecial { get;  set; }

    private async Task Registrar()
    {
        _logger.Info("Registro.Registrar", "Inicio de registro de usuario.");
        try
        {
            EstaCargando = true;
            MensajeError = string.Empty;

            // Validar cupón si fue proporcionado
            if (!string.IsNullOrWhiteSpace(CuponRegistro) && CuponRegistro.Trim().Length <= 3)
            {
                await _toast.MostrarAsync("El cupón debe tener más de 3 caracteres.", ToastIcono.Error, ToastPosicion.Bottom);
                EstaCargando = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(CuponRegistro))
            {
                _logger.Debug("Registro.RegistrarCupon", "Validando cupón de registro.");
                var cuponResult = await _servicioEcommerce.AplicarCupon(CuponRegistro, new ActivacionCuponDto
                {
                    Codigo = CuponRegistro,
                    Activar = false
                });
                if (cuponResult.Codigo is null || cuponResult.Aplicado == true)
                {
                    await _toast.MostrarAsync("Cupón no válido, por favor verifica e intenta de nuevo.", ToastIcono.Error, ToastPosicion.Bottom);
                    EstaCargando = false;
                    return;
                }

                var popup = new AlertaPopup(
                    "¡Bienvenido! 🎉",
                    cuponResult.Descripcion ?? cuponResult.Nombre ?? "Cupón canjeado exitosamente.",
                    verBotonCancelar: false,
                    confirmarText: "Continuar");
                await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);
            }

            // Obtener el ID del dispositivo
            var dispositivoId = await ObtenerDispositivoId();

            var model = new RegisterViewModel
            {
                Email = Email,
                Password = Password,
                DispositivoId = dispositivoId,
                Nombre = Nombre,
                CuponRegistro = string.IsNullOrWhiteSpace(CuponRegistro) ? null : CuponRegistro
            };

            // Llamar al servicio de registro
            var respuesta = await _servicioIdentidad.Registrar(model);

            if (!respuesta.Ok)
            {
                _logger.Debug("Registro.RegistrarError", "La API devolvió error al registrar usuario.", new Dictionary<string, object?>
                {
                    ["HttpCode"] = (int?)respuesta.HttpCode,
                    ["Codigo"] = respuesta.Error?.Codigo,
                    ["Mensaje"] = respuesta.Error?.Mensaje
                });
                throw new Exception(respuesta.HttpCode.ToString());
            }

            _logger.Info("Registro.RegistrarExitoso", "Registro completado correctamente.");

            await _toast.MostrarAsync(
                "Registro completado. Por favor verifica tu correo electrónico.",
                ToastIcono.Info, ToastPosicion.Bottom);

            PaginaLogin.LimpiarAlNavegar = true;
            await IrALogin();
        }
        catch (ApiException ex)
        {
            _logger.Debug("Registro.RegistrarApiException", "Error de API durante el registro.", new Dictionary<string, object?>
            {
                ["StatusCode"] = ex.StatusCode,
                ["Mensaje"] = ex.Message
            });
            var mensaje = ex.StatusCode switch
            {
                409 => "El correo electrónico ya está registrado.",
                500 => "Error al registrar. Por favor intenta más tarde.",
                _ => "Error al registrar. Por favor verifica tus datos."
            };

            await _toast.MostrarAsync(mensaje, ToastIcono.Error, ToastPosicion.Bottom);
            MensajeError = mensaje;
        }
        catch (Exception ex)
        {
            _logger.Debug("Registro.RegistrarException", "Excepción no controlada durante el registro.", ex);
            _logs.Log($"[RegistroViewModel] {ex.GetType().Name}: {ex.Message}");
            await _toast.MostrarAsync(
                "Ocurrió un error inesperado. Intenta de nuevo.",
                ToastIcono.Error);
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task<string> ObtenerDispositivoId()
    {
        try
        {
            _logger.Info("Registro.ObtenerDispositivoId", "Inicio de obtención de identificador de dispositivo.");
            var id = await _deviceService.GetDeviceIdAsync();
            _logger.Info("Registro.ObtenerDispositivoIdExitoso", "Obtención de identificador de dispositivo completada.");
            return id;
        }
        catch (Exception ex)
        {
            _logger.Debug("Registro.ObtenerDispositivoIdException", "Excepción no controlada al obtener identificador de dispositivo.", ex);
            throw;
        }
    }

    private async Task IrALogin()
    {
        try
        {
            _logger.Info("Registro.IrALogin", "Inicio de navegación a login desde registro.");
            await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
            _logger.Info("Registro.IrALoginExitoso", "Navegación a login desde registro completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Registro.IrALoginException", "Excepción no controlada al navegar a login desde registro.", ex);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
