using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Maui.Devices;

namespace ContaBeeMovil.Pages.Equipo;

public record TokenCaracter(string Letra, double Ancho, double Alto, double FontSize);

public class SolicitudTokenViewModel : INotifyPropertyChanged
{
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioSesion _servicioSesion;
    private readonly IServicioToast _toast;
    private readonly IServicioAlerta _alerta;
    private readonly IServicioLogs _logs;
    private readonly AppState _appState;

    private bool _estaCargando;
    private bool _mostrarToken;
    private bool _estaIniciandoSesion;
    private bool _inicioYaEjecutado;
    private string _token = string.Empty;
    private IReadOnlyList<TokenCaracter> _tokenChars = [];
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

    public bool EstaIniciandoSesion
    {
        get => _estaIniciandoSesion;
        set { _estaIniciandoSesion = value; OnPropertyChanged(); }
    }

    public string Token
    {
        get => _token;
        private set
        {
            _token = value;
            OnPropertyChanged();
            TokenChars = ConstruirCaracteres(value);
        }
    }

    public IReadOnlyList<TokenCaracter> TokenChars
    {
        get => _tokenChars;
        private set { _tokenChars = value; OnPropertyChanged(); }
    }

    private static IReadOnlyList<TokenCaracter> ConstruirCaracteres(string token)
    {
        if (string.IsNullOrEmpty(token)) return [];
        try
        {
            var info = DeviceDisplay.MainDisplayInfo;
            var screenWidth = info.Density > 0 ? info.Width / info.Density : 390.0;
            var n = token.Length;
            var spacing = (n - 1) * 8.0;
            var available = screenWidth - 32.0 - spacing; // 16dp padding cada lado
            var ancho = Math.Max(36, Math.Min(84, Math.Floor(available / n)));
            var alto = Math.Round(ancho * 1.38);
            var fontSize = Math.Round(ancho * 0.52);
            return token.Select(c => new TokenCaracter(c.ToString(), ancho, alto, fontSize)).ToList();
        }
        catch
        {
            return token.Select(c => new TokenCaracter(c.ToString(), 48, 66, 30)).ToList();
        }
    }

    public ICommand CancelarCommand { get; }

    public SolicitudTokenViewModel(
        IServicioIdentidad servicioIdentidad,
        IServicioSesion servicioSesion,
        IServicioToast toast,
        IServicioAlerta alerta,
        IServicioLogs logs,
        AppState appState)
    {
        _servicioIdentidad = servicioIdentidad;
        _servicioSesion    = servicioSesion;
        _toast             = toast;
        _alerta            = alerta;
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
        if (_inicioYaEjecutado) return;
        _inicioYaEjecutado = true;

        _enSesion = enSesion;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        EstaCargando = true;
        MostrarToken = false;
        try
        {
            var dispositivoId = await _servicioSesion.LeeIdDeDispositivo();
            _logs.Info($"[Vinculación:{(enSesion ? "ConSesion" : "SinSesion")}] Solicitando token. DispositivoId={LogSanitizer.EnmascararDispositivoId(dispositivoId)}");

            var resultado = await _servicioIdentidad.GetTokenVinculacion(dispositivoId, enSesion);

            if (!resultado.Ok)
            {
                // 409 solo ocurre en flujo sin cuenta: el dispositivo ya tiene un usuario normal registrado.
                // DELETE no requiere JWT en este caso — el backend lo permite sin sesión.
                if (resultado.HttpCode == System.Net.HttpStatusCode.Conflict && !enSesion)
                {
                    var confirmar = await _alerta.MostrarAsync(
                        "Dispositivo con cuenta registrada",
                        "Este dispositivo ya está vinculado a otra cuenta. Para continuar como usuario sin cuenta es necesario liberar el dispositivo.",
                        confirmarText: "Liberar y continuar");

                    if (!confirmar)
                    {
                        await NavegaAtrasAsync();
                        return;
                    }

                    await _servicioIdentidad.EliminarAsociacionesDispositivo(dispositivoId);
                    resultado = await _servicioIdentidad.GetTokenVinculacion(dispositivoId, enSesion);

                    if (!resultado.Ok || resultado.Payload?.Token is null)
                    {
                        _logs.Warn($"[Vinculación] Error tras limpiar dispositivo. Ok={resultado.Ok} Error={resultado.Error?.Codigo}");
                        await NavegaAtrasAsync();
                        _ = _toast.MostrarAsync("No se pudo obtener el token.", ToastIcono.Error);
                        return;
                    }
                }
                else
                {
                    _logs.Warn($"[Vinculación] Error al obtener token. Ok={resultado.Ok} Error={resultado.Error?.Codigo} - {resultado.Error?.Mensaje}");
                    await NavegaAtrasAsync();
                    _ = _toast.MostrarAsync("No se pudo obtener el token.", ToastIcono.Error);
                    return;
                }
            }

            if (resultado.Payload?.Token is null)
            {
                await NavegaAtrasAsync();
                _ = _toast.MostrarAsync("No se pudo obtener el token.", ToastIcono.Error);
                return;
            }

            _logs.Info($"[Vinculación] Token obtenido: {LogSanitizer.IndicarTokenObtenido()}. Iniciando polling cada 10s.");
            Token        = resultado.Payload.Token;
            MostrarToken = true;

            _ = IniciarPollingAsync(dispositivoId, _cts.Token);
        }
        catch (Exception ex)
        {
            _logs.Error($"[Vinculación] Excepción en IniciarAsync: {ex.GetType().Name} - {ex.Message}");
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
        _logs.Info("[ConSesion] Poll.");
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionEnSesion(dispositivoId, _token);
            _logs.Info($"[ConSesion] Respuesta ValidaToken. Ok={r.Ok} HttpCode={r.HttpCode} Error={r.Error?.Codigo}");
            if (!r.Ok) return;

            _cts?.Cancel();
            _logs.Info("[ConSesion] Vinculación detectada. Actualizando asociaciones fiscales.");
            await _servicioSesion.GetAsociacionesFiscalesAsync();
            _logs.Info("[ConSesion] Asociaciones actualizadas. Navegando atrás.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            _ = _toast.MostrarAsync("¡Vinculación exitosa!", ToastIcono.Info);
        }
        catch (Exception ex)
        {
            _logs.Error($"[ConSesion] Excepción: {ex.GetType().Name} - {ex.Message}");
            _cts?.Cancel();
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
            _ = _toast.MostrarAsync("Error al completar la vinculación.", ToastIcono.Error);
        }
    }

    private async Task PollearSinSesionAsync(string dispositivoId)
    {
        _logs.Info("[SinSesion] Poll.");
        try
        {
            var r = await _servicioIdentidad.ValidaTokenVinculacionSinSesion(dispositivoId, _token);
            _logs.Info($"[SinSesion] Respuesta ValidaToken. Ok={r.Ok} HttpCode={r.HttpCode} Error={r.Error?.Codigo}");
            if (!r.Ok) return;

            // El backend borra el registro al devolver 200, así que cancelamos
            // el polling aquí antes de cualquier llamada que pueda fallar.
            _cts?.Cancel();
            await MainThread.InvokeOnMainThreadAsync(() => EstaIniciandoSesion = true);

            _logs.Info("[SinSesion] Vinculación detectada. Solicitando token LoginLess.");
            var loginlessResult = await _servicioIdentidad.GetTokenLoginLess(dispositivoId);
            _logs.Info($"[SinSesion] GetTokenLoginLess. Ok={loginlessResult.Ok} Error={loginlessResult.Error?.Codigo}");
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

            _logs.Info("[SinSesion] Iniciando sesión con token LoginLess.");
            var loginR = await _servicioIdentidad.IniciarSesion(
                loginlessToken, "Password", dispositivoId, recordarme: false);

            _logs.Info($"[SinSesion] IniciarSesion. Ok={loginR.Ok} Error={loginR.Error?.Codigo} - {loginR.Error?.Mensaje}");
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

            _logs.Info("[SinSesion] Token guardado. Ejecutando PosLoginAsync.");
            await _servicioSesion.PosLoginAsync();

            _logs.Info("[SinSesion] PosLogin completado. Navegando a AppShell.");

            // Mostrar PaginaCargando primero para que el usuario no vea negro
            // mientras MAUI construye AppShell (que tarda 2-3s en devices lentos).
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _appState.EsLoginLess = true;
                Application.Current!.Windows[0].Page = new ContaBeeMovil.Pages.PaginaCargando();
            });

            // Ceder el hilo principal para que renderice PaginaCargando
            await Task.Delay(80);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var shell = MauiProgram.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            });
        }
        catch (Exception ex)
        {
            _logs.Error($"[SinSesion] Excepción: {ex.GetType().Name} - {ex.Message}");
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
