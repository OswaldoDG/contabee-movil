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
using ContaBeeMovil.Services.Logging;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IAppLogger _logger;
    private readonly LogContextService _logContextService;
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
        IAppLogger logger,
        LogContextService logContextService)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _toast = toast;
        _almacenamiento = almacenamiento;
        _logger = logger;
        _logContextService = logContextService;
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
        var correlationId = _logContextService.NewCorrelationId();
        _logger.Info("Login.SubmitStarted", "Inicio de intento de login.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));

        _emailTocado = true;
        _passwordTocado = true;
        EmailRequerido = string.IsNullOrWhiteSpace(Email);
        PasswordRequerido = string.IsNullOrWhiteSpace(Password);

        if (EmailRequerido || PasswordRequerido)
        {
            _logger.Info("Login.ValidationFailed", "No se pudo continuar con login por datos incompletos.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));

            var validationDebugContext = _logContextService.BuildCommonContext("PaginaLogin", correlationId);
            validationDebugContext["EmailRequerido"] = EmailRequerido;
            validationDebugContext["PasswordRequerido"] = PasswordRequerido;
            _logger.Debug("Login.ValidationFailed.Details", "Detalle de validación de campos requeridos.", validationDebugContext);
            return;
        }

        try
        {
            EstaCargando = true;
            var stopWatch = Stopwatch.StartNew();

            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            var authStartContext = _logContextService.BuildCommonContext("PaginaLogin", correlationId);
            authStartContext["Recordarme"] = Recordarme;
            _logger.Debug("Login.AuthRequestStarted", "Iniciando autenticación contra API de identidad.", authStartContext);

            var resultado = await _servicioIdentidad.IniciarSesion(Email, Password, dispositivoId, Recordarme);

            if (!resultado.Ok || resultado.Payload == null)
            {
                stopWatch.Stop();
                var mensaje = resultado.Error?.Codigo == "invalid_grant"
                    ? "El correo o la contraseña son incorrectos."
                    : "Ha ocurrido un error al iniciar sesión.";

                _logger.Info("Login.AuthRequestFailed", "No fue posible autenticar al usuario.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));

                var failedDebugContext = _logContextService.BuildCommonContext("PaginaLogin", correlationId);
                failedDebugContext["HttpCode"] = (int?)resultado.HttpCode;
                failedDebugContext["Codigo"] = resultado.Error?.Codigo;
                failedDebugContext["DurationMs"] = stopWatch.ElapsedMilliseconds;
                _logger.Debug("Login.AuthRequestFailed.Details", "Detalle técnico de autenticación fallida.", failedDebugContext);

                await _toast.MostrarAsync(mensaje, ToastIcono.Warning, ToastPosicion.Bottom);
                return;
            }

            stopWatch.Stop();
            var userId = _logContextService.ExtractUserIdFromAccessToken(resultado.Payload.AccessToken);
            _logContextService.SetCurrentUserId(userId);

            _logger.Info("Login.AuthRequestSucceeded", "Autenticación exitosa.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));

            var authSuccessDebugContext = _logContextService.BuildCommonContext("PaginaLogin", correlationId);
            authSuccessDebugContext["DurationMs"] = stopWatch.ElapsedMilliseconds;
            authSuccessDebugContext["UserIdResolved"] = !string.IsNullOrWhiteSpace(userId);
            _logger.Debug("Login.AuthRequestSucceeded.Details", "Detalle técnico de autenticación exitosa.", authSuccessDebugContext);

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
                _logger.Debug("Login.NavigationToAppShell", "Navegación a AppShell después de login exitoso.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
            else
            {
                _logger.Debug("Login.NavigationToRegisterRfc", "Usuario sin cuentas fiscales, navegación a registro RFC.", _logContextService.BuildCommonContext("PaginaLogin", correlationId));
                // Lista vacía = API devolvió 404, usuario sin cuentas fiscales registradas
                var registrarPage = MauiProgram.Services.GetRequiredService<RegistrarRFCsPage>();
                registrarPage.FromLogin = true;
                Application.Current!.Windows[0].Page = registrarPage;
            }
        }
        catch
        {
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
        await _toast.MostrarAsync("La funcionalidad de vinculación estará disponible próximamente.", ToastIcono.Warning, ToastPosicion.Bottom);
    }

    private async Task IrARegistro()
    {
        _logger.Debug("Login.RegisterTapped", "Navegación a registro desde login.", _logContextService.BuildCommonContext("PaginaLogin"));
        var paginaRegistro = App.Services.GetRequiredService<PaginaRegistro>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(paginaRegistro);
    }

    private void RecuperarContrasena()
    {
        _logger.Debug("Login.ForgotPasswordTapped", "Navegación a recuperar contraseña desde login.", _logContextService.BuildCommonContext("PaginaLogin"));
        var pagina = App.Services.GetRequiredService<RecuperarPassPage>();
        _ = Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
    }

    private Task MostrarInfoApp()
    {
        var pagina = App.Services.GetRequiredService<AcercaDePage>();
        return Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
    }

    private Task MostrarInfo()
    {
        var pagina = App.Services.GetRequiredService<AcercaDePage>();
        return Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
    }

    #endregion

    private async Task VerificarModoDeveloperAsync()
    {
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
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
