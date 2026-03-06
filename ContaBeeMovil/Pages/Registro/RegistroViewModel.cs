using Contabee.Api;
using Contabee.Api.Identidad;
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
    private readonly ServicioIdentidadClient _servicioIdentidad;

    public RegistroViewModel()
    {
        // Inicializar el servicio (ajustar según tu configuración de DI)
        // _servicioIdentidad = ...; 

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
            await _servicioIdentidad.RegistroAsync(detalle: true, body: model);

            // Mostrar mensaje de éxito
            await Application.Current.MainPage.DisplayAlert(
                "Éxito",
                "Registro completado. Por favor verifica tu correo electrónico.",
                "OK");

            // Navegar a login
            await IrALogin();
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 409)
            {
                MensajeError = "El correo electrónico ya está registrado.";
            }
            else if (ex.StatusCode == 500)
            {
                MensajeError = "Error del servidor. Por favor intenta más tarde.";
            }
            else
            {
                MensajeError = "Error al registrar. Por favor verifica tus datos.";
            }
        }
        catch (Exception ex)
        {
            MensajeError = $"Error inesperado: {ex.Message}";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task<string> ObtenerDispositivoId()
    {
        // Implementar según tu estrategia de identificación de dispositivo
        // Opciones: GUID persistente en Preferences, DeviceInfo, etc.

        var deviceId = Preferences.Get("DeviceId", string.Empty);

        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = Guid.NewGuid().ToString();
            Preferences.Set("DeviceId", deviceId);
        }

        return deviceId;
    }

    private async Task IrALogin()
    {
        Application.Current!.Windows[0].Page = new ContaBeeMovil.Pages.Login.PaginaLogin();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}