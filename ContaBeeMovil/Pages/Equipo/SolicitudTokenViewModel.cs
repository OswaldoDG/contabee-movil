using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Equipo;

public class SolicitudTokenViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly AppState _appState;

    private bool _estaCargando;
    private bool _mostrarToken;
    private string _token = string.Empty;
    private IReadOnlyList<string> _tokenChars = [];
    private bool _enSesion;
    private CancellationTokenSource? _cts;

    public bool EstaCargando
    {
        get => _estaCargando;
        set { _estaCargando = value; OnPropertyChanged(); }
    }

    public bool MostrarToken
    {
        get => _mostrarToken;
        set { _mostrarToken = value; OnPropertyChanged(); }
    }

    public string Token
    {
        get => _token;
        private set
        {
            _token = value;
            OnPropertyChanged();
            TokenChars = value.Select(c => c.ToString()).ToList();
        }
    }

    public IReadOnlyList<string> TokenChars
    {
        get => _tokenChars;
        private set { _tokenChars = value; OnPropertyChanged(); }
    }

    public ICommand CancelarCommand { get; }

    public SolicitudTokenViewModel(
        IServicioIdentidad servicioIdentidad,
        IServicioSesion servicioSesion,
        IServicioToast toast,
        AppState appState)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion    = servicioSesion;
        _toast             = toast;
        _appState          = appState;

        CancelarCommand = new Command(async () => await CancelarAsync());
    }

    private async Task CancelarAsync()
    {
        _cts?.Cancel();
        await _toast.MostrarAsync("Vinculación cancelada.", ToastIcono.Warning);
        await NavegaAtrasAsync();
    }

    public async Task IniciarAsync(bool enSesion)
    {
        _enSesion = enSesion;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        EstaCargando = true;
        MostrarToken = false;
        try
        {
            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            var resultado = await _servicioIdentidad.GetTokenVinculacion(dispositivoId, enSesion);

            if (!resultado.Ok || resultado.Payload?.Token is null)
            {
                await _toast.MostrarAsync("No se pudo obtener el token.", ToastIcono.Error);
                await NavegaAtrasAsync();
                return;
            }

            Token        = resultado.Payload.Token;
            MostrarToken = true;

            _ = IniciarPollingAsync(dispositivoId, _cts.Token);
        }
        catch
        {
            await _toast.MostrarAsync("Error al solicitar el token.", ToastIcono.Error);
            await NavegaAtrasAsync();
        }
        finally
        {
            EstaCargando = false;
        }
    }

    private async Task IniciarPollingAsync(string dispositivoId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(10_000, ct);

                if (_enSesion)
                    await PollearConSesionAsync(dispositivoId);
                else
                    await PollearSinSesionAsync(dispositivoId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SolicitudToken] Error polling: {ex.Message}");
        }
    }

    private async Task PollearConSesionAsync(string dispositivoId)
    {
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionEnSesion(dispositivoId, _token);
            if (!r.Ok) return;

            _cts?.Cancel();
            await _servicioSesion.GetAsociacionesFiscalesAsync();
            await _toast.MostrarAsync("¡Vinculación exitosa!", ToastIcono.Info);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SolicitudToken:EnSesion] {ex.Message}");
            _cts?.Cancel();
            await _toast.MostrarAsync("Error al completar la vinculación.", ToastIcono.Error);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
    }

    private async Task PollearSinSesionAsync(string dispositivoId)
    {
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionSinSesion(dispositivoId, _token);
            if (!r.Ok) return;

            // El backend borra el registro al devolver 200, así que cancelamos
            // el polling aquí antes de cualquier llamada que pueda fallar.
            _cts?.Cancel();

            var loginlessResult = await _servicioIdentidad.GetTokenLoginLess(dispositivoId);
            if (!loginlessResult.Ok || loginlessResult.Payload?.Token is null)
            {
                await _toast.MostrarAsync("Error al obtener acceso LoginLess.", ToastIcono.Error);
                await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
                return;
            }

            var loginlessToken = loginlessResult.Payload.Token;

            var tokenGuardado = await _servicioSesion.LeeTokenLoginLessAsync();
            if (tokenGuardado != loginlessToken)
                await _servicioSesion.GuardaTokenLoginLessAsync(loginlessToken);

            var loginR = await _servicioIdentidad.IniciarSesion(
                loginlessToken, "Password", dispositivoId, recordarme: false);

            if (!loginR.Ok || loginR.Payload is null)
            {
                await _toast.MostrarAsync("Error al iniciar sesión.", ToastIcono.Error);
                await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
                return;
            }

            await _servicioSesion.GuardaTokenAsync(
                loginR.Payload.AccessToken,
                loginR.Payload.RefreshToken);

            await _servicioSesion.GuardaExpiracionAsync(
                DateTime.Now.AddSeconds(loginR.Payload.ExpiresIn));

            await _servicioSesion.PosLoginAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _appState.EsLoginLess = true;
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SolicitudToken:SinSesion] {ex.Message}");
            await _toast.MostrarAsync("Error al completar la vinculación.", ToastIcono.Error);
            await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
        }
    }

    private Task NavegaAtrasAsync() =>
        _enSesion
            ? Shell.Current.GoToAsync("..")
            : Application.Current!.Windows[0].Page!.Navigation.PopAsync();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
