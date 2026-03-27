using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioNotificacion _notificacion;
    private readonly IToastService toastService;
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
        IServicioNotificacion notificacion,
        IToastService toastService)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _notificacion = notificacion;
        this.toastService = toastService;
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
        // Restaurar estado del checkbox desde AppState
        _recordarme = AppState.Instance.Recordarme;
        OnPropertyChanged(nameof(Recordarme));

        // Siempre cargar el email guardado si existe, independientemente del checkbox
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
        _emailTocado = true;
        _passwordTocado = true;
        EmailRequerido = string.IsNullOrWhiteSpace(Email);
        PasswordRequerido = string.IsNullOrWhiteSpace(Password);

        if (EmailRequerido || PasswordRequerido)
            return;

        try
        {
            EstaCargando = true;

            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            var resultado = await _servicioIdentidad.IniciarSesion(Email, Password, dispositivoId, Recordarme);

            if (!resultado.Ok || resultado.Payload == null)
            {
                var mensaje = resultado.Error?.Codigo == "invalid_grant"
                    ? "El correo o la contraseña son incorrectos."
                    : "Ha ocurrido un error al iniciar sesión.";
                await toastService.ShowAsync(mensaje, type: ToastType.Warning, position: ToastPosition.Bottom);
                return;
            }

            await _servicioSesion.GuardaTokenAsync(
                resultado.Payload.AccessToken,
                resultado.Payload.RefreshToken);

            await _servicioSesion.GuardaExpiracionAsync(
                DateTime.Now.AddSeconds(resultado.Payload.ExpiresIn));

            await _servicioSesion.GuardaEmailAsync(Email);

            await _servicioSesion.PosLoginAsync();

            // Transition animation
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
            // Guardar estado del checkbox en AppState
            AppState.Instance.Recordarme = Recordarme;

            var tieneCuentasFiscales =
                AppState.Instance.CuentasFiscales != null &&
                AppState.Instance.CuentasFiscales.Count > 0;

            if (tieneCuentasFiscales)
            {
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
            else
            {
                var registrarPage = MauiProgram.Services.GetRequiredService<RegistrarRFCsPage>();
                Application.Current!.Windows[0].Page = registrarPage;
            }
        }
        catch 
        {
           // _ = _notificacion.MostrarErrorAsync($"Error al iniciar sesión: {ex.Message}");

            await toastService.ShowAsync("Error al iniciar sesión.", type: ToastType.Warning, position: ToastPosition.Bottom);
            // Reset animation if error
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
       //await  _notificacion.ShowAlert("La funcionalidad de vinculación estará disponible próximamente.");

       await toastService.ShowAsync("La funcionalidad de vinculación estará disponible próximamente.",type: ToastType.Warning,position:ToastPosition.Bottom);
    }

    private async Task IrARegistro()
    {
        var paginaRegistro = App.Services.GetRequiredService<PaginaRegistro>();
        Application.Current!.Windows[0].Page = paginaRegistro;
    }
    private void RecuperarContrasena()
    {
        var pagina = App.Services.GetRequiredService<RecuperarPassPage>();
        Application.Current!.Windows[0].Page = pagina;
    }

    private async Task MostrarInfoApp()
    {
       // await _notificacion.MostrarInfoAsync("ContaBee - Sistema de contabilidad móvil.");
        await toastService.ShowAsync("ContaBee - Sistema de contabilidad móvil.", type: ToastType.Warning, position: ToastPosition.Bottom);
    }

    private async Task MostrarInfo()
    {
        //await _notificacion.MostrarInfoAsync("ContaBee v1.0 — Desarrollado por ContaBee.");
        await toastService.ShowAsync("ContaBee v1.0 — Desarrollado por ContaBee.", type: ToastType.Warning, position: ToastPosition.Bottom);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
