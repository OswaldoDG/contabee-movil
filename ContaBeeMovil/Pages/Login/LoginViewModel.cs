using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.Perfil;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Pages.Registro;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Login;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioNotificacion _notificacion;
    private readonly IServicioToast _toast;
    private readonly IServicioAlmacenamiento _almacenamiento;
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
        IServicioNotificacion notificacion,
        IServicioToast toast,
        IServicioAlmacenamiento almacenamiento)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _notificacion = notificacion;
        _toast = toast;
        _almacenamiento = almacenamiento;
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
                await _toast.MostrarAsync(mensaje, ToastIcono.Warning, ToastPosicion.Bottom);
                return;
            }

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
        var paginaRegistro = App.Services.GetRequiredService<PaginaRegistro>();
        await Application.Current!.Windows[0].Page!.Navigation.PushAsync(paginaRegistro);
    }

    private void RecuperarContrasena()
    {
        var pagina = App.Services.GetRequiredService<RecuperarPassPage>();
        _ = Application.Current!.Windows[0].Page!.Navigation.PushAsync(pagina);
    }

    private async Task MostrarInfoApp()
    {
        await _toast.MostrarAsync("ContaBee - Sistema de contabilidad móvil.", ToastIcono.Info, ToastPosicion.Bottom);
    }

    private async Task MostrarInfo()
    {
        var version = AppInfo.Current.VersionString;
        var build = AppInfo.Current.BuildString;
        await _toast.MostrarAsync($"ContaBee v{version} ({build})", ToastIcono.Info, ToastPosicion.Bottom);
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
