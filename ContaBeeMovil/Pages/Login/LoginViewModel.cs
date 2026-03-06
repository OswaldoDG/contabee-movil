using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services;

namespace ContaBeeMovil.Pages.Login;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioNotificacion _notificacion;

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
        IServicioNotificacion notificacion)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion = servicioSesion;
        _notificacion = notificacion;

        IngresarCommand = new Command(async () => await Ingresar(), () => PuedeIngresar);
        VincularmeCommand = new Command(async () => await Vincularme());
        IrARegistroCommand = new Command(async () => await IrARegistro());
        RecuperarContrasenaCommand = new Command(async () => await RecuperarContrasena());
        MostrarInfoAppCommand = new Command(async () => await MostrarInfoApp());
        MostrarInfoCommand = new Command(async () => await MostrarInfo());

        _ = CargarCredencialesAsync();
    }

    private async Task CargarCredencialesAsync()
    {
        var email = await _servicioSesion.LeeEmailAsync();
        if (!string.IsNullOrEmpty(email))
        {
            _email = email;
            _recordarme = true;
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(Recordarme));
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
            var resultado = await _servicioIdentidad.IniciarSesion(Email, Password, dispositivoId);

            if (!resultado.Ok || resultado.Payload == null)
            {
                _ = _notificacion.MostrarErrorAsync(
                    resultado.Error?.Mensaje ?? "Error al iniciar sesión");
                return;
            }

            await _servicioSesion.GuardaTokenAsync(
                resultado.Payload.AccessToken,
                resultado.Payload.RefreshToken);

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

            if (Recordarme)
            {
                await _servicioSesion.GuardaEmailAsync(Email);
            }
            else
            {
                await _servicioSesion.LimpiaEmailAsync();
            }

            Application.Current!.Windows[0].Page = new AppShell();
        }
        catch (Exception ex)
        {
            _ = _notificacion.MostrarErrorAsync($"Error al iniciar sesión: {ex.Message}");

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
        await _notificacion.MostrarInfoAsync(
            "La funcionalidad de vinculación estará disponible próximamente.");
    }

    private Task IrARegistro()
    {
        Application.Current!.Windows[0].Page = new ContaBeeMovil.Pages.Registro.PaginaRegistro();
        return Task.CompletedTask;
    }

    private async Task RecuperarContrasena()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            _ = _notificacion.MostrarErrorAsync(
                "Ingresa tu correo electrónico para recuperar tu contraseña.");
            return;
        }

        await _notificacion.MostrarInfoAsync(
            $"Se enviará un enlace de recuperación a {Email}.");
    }

    private async Task MostrarInfoApp()
    {
        await _notificacion.MostrarInfoAsync("ContaBee - Sistema de contabilidad móvil.");
    }

    private async Task MostrarInfo()
    {
        await _notificacion.MostrarInfoAsync("ContaBee v1.0 — Desarrollado por ContaBee.");
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
