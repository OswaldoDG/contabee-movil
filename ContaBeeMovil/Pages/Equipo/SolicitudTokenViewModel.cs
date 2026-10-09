using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;

namespace ContaBeeMovil.Pages.Equipo;

public class SolicitudTokenViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly IServicioLogs _logs;
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
        IServicioLogs logs,
        AppState appState)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion    = servicioSesion;
        _toast             = toast;
        _logs              = logs;
        _appState          = appState;

        CancelarCommand = new Command(async () => await CancelarAsync());
    }

    private async Task CancelarAsync()
    {
        _cts?.Cancel();
        await NavegaAtrasAsync();
        _ = _toast.MostrarAsync("Vinculación cancelada.", ToastIcono.Warning);
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
            _logs.Log($"[Vinculación:{(enSesion ? "ConSesion" : "SinSesion")}] Solicitando token. DispositivoId={dispositivoId}");

            var resultado = await _servicioIdentidad.GetTokenVinculacion(dispositivoId, enSesion);

            if (!resultado.Ok || resultado.Payload?.Token is null)
            {
                _logs.Log($"[Vinculación] Error al obtener token. Ok={resultado.Ok} Error={resultado.Error?.Codigo} - {resultado.Error?.Mensaje}");
                await NavegaAtrasAsync();
                _ = _toast.MostrarAsync("No se pudo obtener el token.", ToastIcono.Error);
                return;
            }

            _logs.Log($"[Vinculación] Token obtenido: {resultado.Payload.Token}. Iniciando polling cada 10s.");
            Token        = resultado.Payload.Token;
            MostrarToken = true;

            _ = IniciarPollingAsync(dispositivoId, _cts.Token);
        }
        catch (Exception ex)
        {
            _logs.Log($"[Vinculación] Excepción en IniciarAsync: {ex.GetType().Name} - {ex.Message}");
            await NavegaAtrasAsync();
            _ = _toast.MostrarAsync("Error al solicitar el token.", ToastIcono.Error);
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
        _logs.Log($"[ConSesion] Poll. Token={_token}");
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionEnSesion(dispositivoId, _token);
            _logs.Log($"[ConSesion] Respuesta ValidaToken. Ok={r.Ok} HttpCode={r.HttpCode} Error={r.Error?.Codigo}");
            if (!r.Ok) return;

            _cts?.Cancel();
            _logs.Log("[ConSesion] Vinculación detectada. Actualizando asociaciones fiscales.");
            await _servicioSesion.GetAsociacionesFiscalesAsync();
            _logs.Log("[ConSesion] Asociaciones actualizadas. Navegando atrás.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            _ = _toast.MostrarAsync("¡Vinculación exitosa!", ToastIcono.Info);
        }
        catch (Exception ex)
        {
            _logs.Log($"[ConSesion] Excepción: {ex.GetType().Name} - {ex.Message}");
            _cts?.Cancel();
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            _ = _toast.MostrarAsync("Error al completar la vinculación.", ToastIcono.Error);
        }
    }

    private async Task PollearSinSesionAsync(string dispositivoId)
    {
        _logs.Log($"[SinSesion] Poll. Token={_token}");
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionSinSesion(dispositivoId, _token);
            _logs.Log($"[SinSesion] Respuesta ValidaToken. Ok={r.Ok} HttpCode={r.HttpCode} Error={r.Error?.Codigo}");
            if (!r.Ok) return;

            // El backend borra el registro al devolver 200, así que cancelamos
            // el polling aquí antes de cualquier llamada que pueda fallar.
            _cts?.Cancel();

            _logs.Log("[SinSesion] Vinculación detectada. Solicitando token LoginLess.");
            var loginlessResult = await _servicioIdentidad.GetTokenLoginLess(dispositivoId);
            _logs.Log($"[SinSesion] GetTokenLoginLess. Ok={loginlessResult.Ok} Token={loginlessResult.Payload?.Token} Error={loginlessResult.Error?.Codigo}");
            if (!loginlessResult.Ok || loginlessResult.Payload?.Token is null)
            {
                await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
                _ = _toast.MostrarAsync("Error al obtener acceso LoginLess.", ToastIcono.Error);
                return;
            }

            var loginlessToken = loginlessResult.Payload.Token;

            var tokenGuardado = await _servicioSesion.LeeTokenLoginLessAsync();
            if (tokenGuardado != loginlessToken)
                await _servicioSesion.GuardaTokenLoginLessAsync(loginlessToken);

            _logs.Log("[SinSesion] Iniciando sesión con token LoginLess.");
            var loginR = await _servicioIdentidad.IniciarSesion(
                loginlessToken, "Password", dispositivoId, recordarme: false);

            _logs.Log($"[SinSesion] IniciarSesion. Ok={loginR.Ok} Error={loginR.Error?.Codigo} - {loginR.Error?.Mensaje}");
            if (!loginR.Ok || loginR.Payload is null)
            {
                await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
                _ = _toast.MostrarAsync("Error al iniciar sesión.", ToastIcono.Error);
                return;
            }

            await _servicioSesion.GuardaTokenAsync(
                loginR.Payload.AccessToken,
                loginR.Payload.RefreshToken);

            await _servicioSesion.GuardaExpiracionAsync(
                DateTime.Now.AddSeconds(loginR.Payload.ExpiresIn));

            _logs.Log("[SinSesion] Token guardado. Ejecutando PosLoginAsync.");
            await _servicioSesion.PosLoginAsync();

            _logs.Log("[SinSesion] PosLogin completado. Navegando a AppShell.");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _appState.EsLoginLess = true;
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            });
        }
        catch (Exception ex)
        {
            _logs.Log($"[SinSesion] Excepción: {ex.GetType().Name} - {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(NavegaAtrasAsync);
            _ = _toast.MostrarAsync("Error al completar la vinculación.", ToastIcono.Error);
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
