using System.Diagnostics;
using ContaBee.Services;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Services;

public interface IServicioSalud
{
    Task<bool> VerificarServiciosAsync();

    /// <summary>
    /// Mantiene prendidos los pods del API mientras la app está al frente (App.OnStart/OnResume).
    /// </summary>
    void IniciarCalentamiento();

    /// <summary>Detiene el calentamiento al pasar a segundo plano (App.OnSleep).</summary>
    void DetenerCalentamiento();
}

public class ServicioSalud(IHttpClientFactory factory, IServicioLogs logs) : IServicioSalud
{
    // Azure apaga un pod tras ~5 min sin tráfico y tarda ~10 s en prenderlo; como los pods
    // se llaman entre sí, una llamada del usuario encadena varios arranques en frío en serie.
    // Pegarle a todos en paralelo al abrir la app, y cada 4 min mientras siga al frente,
    // hace que el usuario los encuentre ya prendidos.
    private static readonly TimeSpan _intervaloCalentamiento = TimeSpan.FromMinutes(4);

    private readonly Lock _lock = new();
    private CancellationTokenSource? _ciclo;
    private DateTime _ultimoPing = DateTime.MinValue;

    private static string[] Endpoints()
    {
        var config = ServicioConfiguracion.Actual;
        return
        [
            $"{config.UrlIdentity}health",
            $"{config.UrlCrm}health",
            $"{config.UrlTranscript}health",
            $"{config.UrlEcommerce}health",
        ];
    }

    public async Task<bool> VerificarServiciosAsync()
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(6);

        var tareas = Endpoints().Select(async url =>
        {
            try
            {
                using var res = await client.GetAsync(url);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        });

        var resultados = await Task.WhenAll(tareas);
        return resultados.All(ok => ok);
    }

    public void IniciarCalentamiento()
    {
        CancellationToken token;
        lock (_lock)
        {
            _ciclo?.Cancel();
            _ciclo = new CancellationTokenSource();
            token = _ciclo.Token;
        }
        _ = CicloCalentamientoAsync(token);
    }

    public void DetenerCalentamiento()
    {
        lock (_lock)
        {
            _ciclo?.Cancel();
            _ciclo = null;
        }
    }

    private async Task CicloCalentamientoAsync(CancellationToken token)
    {
        try
        {
            // Cámara, selector de fotos y share disparan OnSleep/OnResume seguidos: si el
            // último ping es reciente, se espera a que toque en vez de mandar una ráfaga.
            var restante = _intervaloCalentamiento - (DateTime.UtcNow - _ultimoPing);
            if (restante > TimeSpan.Zero)
                await Task.Delay(restante, token);

            using var timer = new PeriodicTimer(_intervaloCalentamiento);
            do await PingCalentamientoAsync(token);
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logs.Warn($"[Salud] Calentamiento detenido: {ex.Message}");
        }
    }

    private async Task PingCalentamientoAsync(CancellationToken token)
    {
        var access = Connectivity.Current.NetworkAccess;
        if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
            return;

        _ultimoPing = DateTime.UtcNow;

        // Sin AuthHandler: no toca tokens ni el InterruptorApi. El fin es despertar, no
        // verificar, así que el timeout es holgado y el resultado solo se loguea.
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);

        // Todos en paralelo y directo a cada pod. No se usa {transcript}/diagnostico: recorre
        // los hosts de Consul uno tras otro, así que su respuesta tarda la suma de los arranques.
        var tiempos = await Task.WhenAll(Endpoints().Select(async url =>
        {
            var reloj = Stopwatch.StartNew();
            var ruta = new Uri(url).AbsolutePath;
            try
            {
                using var res = await client.GetAsync(url, token);
                return $"{ruta} {(int)res.StatusCode} {reloj.ElapsedMilliseconds}ms";
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                return $"{ruta} {ex.GetType().Name} {reloj.ElapsedMilliseconds}ms";
            }
        }));

        logs.Info($"[Salud] Calentamiento: {string.Join(", ", tiempos)}");
    }
}
