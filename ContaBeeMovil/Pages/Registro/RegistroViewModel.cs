using Contabee.Api;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using ContaBeeMovil;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
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
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IToastService _toast;
    private readonly DeviceService _deviceService;

    public RegistroViewModel(IServicioIdentidad servicioIdentidad, IToastService ToastService,DeviceService deviceService)
    {
        _servicioIdentidad = servicioIdentidad;
        _toast = ToastService;
        _deviceService = deviceService;
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
            ((Command)RegistrarCommand).ChangeCanExecute();
        }
    }

    public bool PuedeRegistrar =>
        !EstaCargando &&
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
        try
        {
            EstaCargando = true;
            MensajeError = string.Empty;

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
           var respuesta =  await _servicioIdentidad.Registrar(model);

           if(!respuesta.Ok)
            {
                throw new Exception(respuesta.HttpCode.ToString());
            }

            await _toast.ShowAsync(
                "Registro completado. Por favor verifica tu correo electrónico.",
                ToastType.Success, position: ToastPosition.Bottom);

            PaginaLogin.LimpiarAlNavegar = true;
            await IrALogin();
        }
        catch (ApiException ex)
        {
            var mensaje = ex.StatusCode switch
            {
                409 => "El correo electrónico ya está registrado.",
                500 => "Error al registrar. Por favor intenta más tarde.",
                _ => "Error al registrar. Por favor verifica tus datos."
            };

            await _toast.ShowAsync(mensaje, ToastType.Error);
            MensajeError = mensaje;
        }
        catch (Exception ex)
        {
            await _toast.ShowAsync(
                $"Error inesperado: {ex.Message}",
                ToastType.Error);
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task<string> ObtenerDispositivoId()
    {
        return await _deviceService.GetDeviceIdAsync();
       
    }

    private async Task IrALogin()
    {
        await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}