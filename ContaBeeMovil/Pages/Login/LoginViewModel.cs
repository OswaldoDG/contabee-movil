using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.AcercaDe;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
using Contabee.Api.Logging;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IAppLogger _logger;
    private const string ClaveMododDev = "ModoDeveloper";
    private string _email = string.Empty;
    private string _password = string.Empty;
    private bool _recordarme;
    private bool _estaCargando;
    private bool _emailRequerido;
    private bool _passwordRequerido;
    private bool _emailTocado;
    private bool _passwordTocado;

    public LoginViewModel(
        IServicioIdentidad servicioIdentidad,
        IServicioSesion servicioSesion,
        IServicioToast toast,
        IServicioAlmacenamiento almacenamiento,
        IAppLogger logger)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _toast = toast;
        _almacenamiento = almacenamiento;
        _logger = logger;
        IngresarCommand = new Command(async () => await Ingresar(), () => PuedeIngresar);
        VincularmeCommand = new Command(async () => await Vincularme());
        IrARegistroCommand = new Command(async () => await IrARegistro());
        RecuperarContrasenaCommand = new Command(RecuperarContrasena);
        MostrarInfoAppCommand = new Command(async () => await MostrarInfoApp());
        MostrarInfoCommand = new Command(async () => await MostrarInfo());

        _ = CargarCredencialesAsync();
    }

    private async Task CargarCredencialesAsync()
    {
        try
        {
            _logger.Info("Login.CargarCredenciales", "Inicio de carga de credenciales recordadas.");
            if (PaginaLogin.LimpiarAlNavegar) return;

            _recordarme = AppState.Instance.Recordarme;
            OnPropertyChanged(nameof(Recordarme));

            var email = await _servicioSesion.LeeEmailAsync();
            if (!string.IsNullOrEmpty(email))
            {
                _email = email;
                OnPropertyChanged(nameof(Email));
                ((Command)IngresarCommand).ChangeCanExecute();
            }

            _logger.Info("Login.CargarCredencialesExitoso", "Carga de credenciales recordadas completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.CargarCredencialesException", "Excepción no controlada al cargar credenciales recordadas.", ex);
        }
    }

    #region Properties

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            _emailTocado = true;
            OnPropertyChanged();
            if (_emailTocado) EmailRequerido = string.IsNullOrWhiteSpace(value);
            ((Command)IngresarCommand).ChangeCanExecute();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            _passwordTocado = true;
            OnPropertyChanged();
            if (_passwordTocado) PasswordRequerido = string.IsNullOrWhiteSpace(value);
            ((Command)IngresarCommand).ChangeCanExecute();
        }
    }

    public bool Recordarme
    {
        get => _recordarme;
        set
        {
            _recordarme = value;
            OnPropertyChanged();
        }
    }

    public bool EmailRequerido
    {
        get => _emailRequerido;
        set { _emailRequerido = value; OnPropertyChanged(); }
    }

    public bool PasswordRequerido
    {
        get => _passwordRequerido;
        set { _passwordRequerido = value; OnPropertyChanged(); }
    }

    public void LimpiarCampos()
    {
        _email = string.Empty;
        _password = string.Empty;
        _emailTocado = false;
        _passwordTocado = false;
        _emailRequerido = false;
        _passwordRequerido = false;
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(EmailRequerido));
        OnPropertyChanged(nameof(PasswordRequerido));
        ((Command)IngresarCommand).ChangeCanExecute();
    }

    public bool EstaCargando
    {
        get => _estaCargando;
        set
        {
            _estaCargando = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormHabilitado));
            ((Command)IngresarCommand).ChangeCanExecute();
        }
    }

    public bool FormHabilitado => !EstaCargando;

    public bool PuedeIngresar =>
        !EstaCargando &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);

    #endregion

    #region Commands

    public ICommand IngresarCommand { get; }
    public ICommand VincularmeCommand { get; }
    public ICommand IrARegistroCommand { get; }
    public ICommand RecuperarContrasenaCommand { get; }
    public ICommand MostrarInfoAppCommand { get; }
    public ICommand MostrarInfoCommand { get; }

    #endregion

    #region Command Handlers

    private async Task Ingresar()
    {
        _logger.Info("Login.Ingresar", "Inicio de intento de login.");

        _emailTocado = true;
        _passwordTocado = true;
        EmailRequerido = string.IsNullOrWhiteSpace(Email);
        PasswordRequerido = string.IsNullOrWhiteSpace(Password);

        if (EmailRequerido || PasswordRequerido)
        {
            _logger.Info("Login.IngresarValidacion", "No se pudo continuar con login por datos incompletos.");

            var validationDebugContext = new Dictionary<string, object?>
            {
                ["EmailRequerido"] = EmailRequerido,
                ["PasswordRequerido"] = PasswordRequerido
            };
            _logger.Debug("Login.IngresarValidacionDetalle", "Detalle de validación de campos requeridos.", validationDebugContext);
            return;
        }

        try
        {
            EstaCargando = true;
            var stopWatch = Stopwatch.StartNew();

            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            var authStartContext = new Dictionary<string, object?> { ["Recordarme"] = Recordarme };
            _logger.Debug("Login.IngresarAutenticacion", "Iniciando autenticación contra API de identidad.", authStartContext);

            var resultado = await _servicioIdentidad.IniciarSesion(Email, Password, dispositivoId, Recordarme);

            if (!resultado.Ok || resultado.Payload == null)
            {
                stopWatch.Stop();
                var mensaje = resultado.Error?.Codigo == "invalid_grant"
                    ? "El correo o la contraseña son incorrectos."
                    : "Ha ocurrido un error al iniciar sesión.";

                _logger.Info("Login.IngresarAutenticacionError", "No fue posible autenticar al usuario.");

                var failedDebugContext = new Dictionary<string, object?>
                {
                    ["HttpCode"] = (int?)resultado.HttpCode,
                    ["Codigo"] = resultado.Error?.Codigo,
                    ["Mensaje"] = resultado.Error?.Mensaje,
                    ["DurationMs"] = stopWatch.ElapsedMilliseconds
                };
                _logger.Debug("Login.IngresarAutenticacionErrorDetalle", "Detalle técnico de autenticación fallida.", failedDebugContext);

                await _toast.MostrarAsync(mensaje, ToastIcono.Warning, ToastPosicion.Bottom);
                return;
            }

            stopWatch.Stop();
            _logger.Info("Login.IngresarExitoso", "Autenticación exitosa.");

            var authSuccessDebugContext = new Dictionary<string, object?>
            {
                ["DurationMs"] = stopWatch.ElapsedMilliseconds
            };
            _logger.Debug("Login.IngresarExitosoDetalle", "Detalle técnico de autenticación exitosa.", authSuccessDebugContext);

            await _servicioSesion.GuardaTokenAsync(
                resultado.Payload.AccessToken,
                resultado.Payload.RefreshToken);

            await _servicioSesion.GuardaExpiracionAsync(
                DateTime.Now.AddSeconds(resultado.Payload.ExpiresIn));

            await _servicioSesion.GuardaEmailAsync(Email);

            await _servicioSesion.PosLoginAsync();

            await VerificarModoDeveloperAsync();

            var page = Application.Current?.Windows[0].Page as ContentPage;
            var formContainer = page?.FindByName<VerticalStackLayout>("FormContainer");
            var logoImage = page?.FindByName<Image>("LogoImage");

            if (formContainer != null)
            {
                var logoTask = logoImage?.ScaleToAsync(0.8, 200, Easing.CubicIn) ?? Task.CompletedTask;
                var slideTask = formContainer.TranslateToAsync(-page!.Width, 0, 400, Easing.CubicIn);
                var fadeTask = formContainer.FadeToAsync(0, 350, Easing.CubicIn);
                await Task.WhenAll(logoTask, slideTask, fadeTask);
            }

            AppState.Instance.Recordarme = Recordarme;

            var cuentas = AppState.Instance.CuentasFiscales;

            // null significa que ocurrió un error y ForzarReloginAsync ya navegó al login
            if (cuentas == null)
                return;

            if (cuentas.Count > 0)
            {
                _logger.Debug("Login.NavigationToAppShell", "Navegación a AppShell después de login exitoso.");
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
            else
            {
                _logger.Debug("Login.NavigationToRegisterRfc", "Usuario sin cuentas fiscales, navegación a registro RFC.");
                // Lista vacía = API devolvió 404, usuario sin cuentas fiscales registradas
                var registrarPage = MauiProgram.Services.GetRequiredService<RegistrarRFCsPage>();
                registrarPage.FromLogin = true;
                Application.Current!.Windows[0].Page = registrarPage;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.IngresarException", "Excepción no controlada al iniciar sesión.", ex);
            await _toast.MostrarAsync("Error al iniciar sesión.", ToastIcono.Warning, ToastPosicion.Bottom);

            var page = Application.Current?.Windows[0].Page as ContentPage;
            var formContainer = page?.FindByName<VerticalStackLayout>("FormContainer");
            var logoImage = page?.FindByName<Image>("LogoImage");

            if (formContainer != null)
            {
                await Task.WhenAll(
                    formContainer.TranslateToAsync(0, 0, 300, Easing.CubicOut),
                    formContainer.FadeToAsync(1, 300, Easing.CubicOut),
                    logoImage?.ScaleToAsync(1, 300, Easing.CubicOut) ?? Task.CompletedTask
                );
            }
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task Vincularme()
    {
        try
        {
            _logger.Info("Login.Vincularme", "Acción de vinculación seleccionada (funcionalidad pendiente).");
            await _toast.MostrarAsync("La funcionalidad de vinculación estará disponible próximamente.", ToastIcono.Warning, ToastPosicion.Bottom);
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.VincularmeException", "Excepción no controlada al mostrar mensaje de vinculación.", ex);
        }
    }

    private async Task IrARegistro()
    {
        try
        {
            _logger.Info("Login.IrARegistro", "Inicio de navegación a pantalla de registro.");
            var paginaRegistro = App.Services.GetRequiredService<PaginaRegistro>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(paginaRegistro);
            _logger.Info("Login.IrARegistroExitoso", "Navegación a pantalla de registro completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.IrARegistroException", "Excepción no controlada al navegar a registro.", ex);
        }
    }

    private void RecuperarContrasena()
    {
        _ = RecuperarContrasenaAsync();
    }

    private async Task RecuperarContrasenaAsync()
    {
        try
        {
            _logger.Info("Login.RecuperarContrasena", "Inicio de navegación a recuperar contraseña.");
            var pagina = App.Services.GetRequiredService<RecuperarPassPage>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
            _logger.Info("Login.RecuperarContrasenaExitoso", "Navegación a recuperar contraseña completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.RecuperarContrasenaException", "Excepción no controlada al navegar a recuperar contraseña.", ex);
        }
    }

    private async Task MostrarInfoApp()
    {
        try
        {
            _logger.Info("Login.MostrarInfoApp", "Inicio de navegación a pantalla Acerca de.");
            var pagina = App.Services.GetRequiredService<AcercaDePage>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
            _logger.Info("Login.MostrarInfoAppExitoso", "Navegación a pantalla Acerca de completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.MostrarInfoAppException", "Excepción no controlada al navegar a pantalla Acerca de.", ex);
        }
    }

    private async Task MostrarInfo()
    {
        try
        {
            _logger.Info("Login.MostrarInfo", "Inicio de navegación a información de la app.");
            var pagina = App.Services.GetRequiredService<AcercaDePage>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
            _logger.Info("Login.MostrarInfoExitoso", "Navegación a información de la app completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.MostrarInfoException", "Excepción no controlada al navegar a información de la app.", ex);
        }
    }

    #endregion

    private async Task VerificarModoDeveloperAsync()
    {
        try
        {
            _logger.Info("Login.VerificarModoDeveloper", "Inicio de verificación de modo desarrollador.");
            var dto = await _almacenamiento.LeerSeguroAsync<ModoDeveloperDto>(ClaveMododDev);
            if (dto is { EsDev: true } &&
                DateTime.TryParse(dto.FechaActivacion, null, DateTimeStyles.RoundtripKind, out var fecha) &&
                (DateTime.UtcNow - fecha).TotalDays <= 30)
            {
                AppState.Instance.EsDev = true;
            }
            else
            {
                AppState.Instance.EsDev = false;
            }
            _logger.Info("Login.VerificarModoDeveloperExitoso", "Verificación de modo desarrollador completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("Login.VerificarModoDeveloperException", "Excepción no controlada al verificar modo desarrollador.", ex);
            AppState.Instance.EsDev = false;
        }
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
