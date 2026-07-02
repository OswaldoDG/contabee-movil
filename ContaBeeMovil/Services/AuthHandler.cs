using System.Net.Http.Headers;
using System.Text.Json;
using Contabee.Api.Identidad;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Services.Sesion;
using Microsoft.Extensions.DependencyInjection;

namespace ContaBeeMovil.Services;

public class AuthHandler : DelegatingHandler
{
    private static readonly string[] _rutasPublicas =
    [
        "/usuario/registro",
        "/api/identity/usuario/registro",
        "/usuario/contrasena/recuperar",
        "/api/identity/usuario/contrasena/recuperar",
        "/usuario/contrasena/restablecer",
        "/api/identity/usuario/contrasena/restablecer",
        "/usuario/registro/confirmar",
        "/api/identity/usuario/registro/confirmar",
        "/api/identity/connect/token",
        "/cupones/validar/",
        "/api/ecommerce/cupones/validar/",
        "/connect/token",
        "/usuarios/tokenvinculacion/",
        "/api/identity/usuarios/tokenvinculacion/"
    ];

    private static readonly string[] _rutasPublicasGet =
    [
        "/usuarios/tokenloginless/",
        "/api/identity/usuarios/tokenloginless/"
    ];

    private static readonly string[] _rutasPublicasDelete =
    [
        "/usuarios/dispositivo/",
        "/api/identity/usuarios/dispositivo/"
    ];

    private readonly IServiceProvider _serviceProvider;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IServicioLogs? _logs;
    private IServicioLogs Logs => _logs ??= _serviceProvider.GetRequiredService<IServicioLogs>();
    private ICoordinadorSesion? _coordinador;
    private ICoordinadorSesion Coordinador => _coordinador ??= _serviceProvider.GetRequiredService<ICoordinadorSesion>();

    public AuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        bool esPublica = (_rutasPublicas.Any(r => path.StartsWith(r)) && !path.EndsWith("/vincular") && !path.EndsWith("/vinculado") && !path.Contains("/tokenvinculacion/sesion"))
            || (request.Method == HttpMethod.Get    && _rutasPublicasGet.Any(r    => path.StartsWith(r)))
            || (request.Method == HttpMethod.Delete && _rutasPublicasDelete.Any(r => path.StartsWith(r)));

        if (esPublica)
            return await base.SendAsync(request, cancellationToken);

        // Verificar conectividad antes de cualquier llamada autenticada
        var networkAccess = Connectivity.Current.NetworkAccess;
        if (networkAccess is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
        {
            Logs.Warn($"[AuthHandler] Sin red — {request.Method} {path}");
            await ActivarModoOfflineAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }

        var sesion = _serviceProvider.GetRequiredService<IServicioSesion>();
        var appState = _serviceProvider.GetRequiredService<AppState>();
        var token = await sesion.LeeAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);

        var expiracion = await sesion.LeeExpiracionAsync();
        bool tokenExpirado = expiracion.HasValue && DateTime.Now >= expiracion.Value;

        if (tokenExpirado)
        {
            var refreshToken = await sesion.LeeRefreshTokenAsync();
            bool esLoginLess = !string.IsNullOrEmpty(await sesion.LeeTokenLoginLessAsync());
            bool puedeRefrescar = (appState.Recordarme || esLoginLess) && !string.IsNullOrEmpty(refreshToken);

            if (puedeRefrescar)
            {
                Logs.Info($"[AuthHandler] Token expirado — intentando refresh. Recordarme={appState.Recordarme} EsLoginLess={esLoginLess}");
                var nuevoToken = await RefrescarTokenAsync(sesion, refreshToken!, cancellationToken);
                if (nuevoToken != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nuevoToken);
                }
                else
                {
                    // Refresh falló con red disponible → token muerto.
                    Logs.Warn("[AuthHandler] Refresh falló — token revocado");
                    await Coordinador.CerrarSesionAsync(MotivoCierre.TokenRevocado);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                }
            }
            else
            {
                // Loginless sin refresh token (instalación legacy anterior al fix de
                // offline_access): NO borrar el token loginless — es recuperable. Diferimos
                // a la reanudación (OnResume/arranque), que re-loguea con el token loginless
                // y sana la sesión con un refresh token nuevo.
                if (esLoginLess)
                {
                    Logs.Warn("[AuthHandler] Loginless sin refresh — se conserva token; sanará al reanudar");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                }

                // Normal sin Recordarme: expiración definitiva → cerrar sesión.
                Logs.Info("[AuthHandler] Token expirado sin posibilidad de refresh");
                await Coordinador.CerrarSesionAsync(MotivoCierre.ExpiradoSinRefresh);
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            }
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Si llegamos aquí, el servidor respondió — hay internet.
            // Limpiar offline mode por si ConnectivityChanged no lo hizo.
            if (AppState.Instance.ModoOffline)
                AppState.Instance.ModoOffline = false;

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    response.Content = new StringContent(
                        body,
                        System.Text.Encoding.UTF8,
                        response.Content.Headers.ContentType?.MediaType ?? "application/json");

                    // DIAGNÓSTICO: registrar el body crudo de toda respuesta no-exitosa para
                    // verificar qué manda el backend (p.ej. diferenciar asociación desactivada
                    // vs eliminada en el 403).
                    Logs.Info($"[AuthHandler] {(int)response.StatusCode} {request.Method} {path} — Body: {(string.IsNullOrEmpty(body) ? "(vacío)" : body)}");

                    // Punto único de manejo de respuestas no-exitosas: el coordinador
                    // decide (403 = asociación desactivada, o mensaje legacy en el body).
                    // Fire-and-forget: la respuesta se devuelve al llamador de inmediato.
                    _ = Coordinador.ManejarRespuestaAsync(response.StatusCode, body, path);
                }
                catch { }
            }

            return response;
        }
        catch (HttpRequestException) when (Connectivity.Current.NetworkAccess is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
        {
            // La red se cayó entre el check inicial y la petición efectiva
            await ActivarModoOfflineAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<string?> RefrescarTokenAsync(
        IServicioSesion sesion, string refreshToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after lock: another concurrent request may have already refreshed
            var expiracion = await sesion.LeeExpiracionAsync();
            if (expiracion.HasValue && DateTime.Now < expiracion.Value)
                return await sesion.LeeAccessTokenAsync();

            Logs.Info("[AuthHandler] Ejecutando refresh de token");

            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("IdentityToken");

            var dispositivoId = await sesion.LeeIdDeDispositivo();
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "contabee-password",
                ["refresh_token"] = refreshToken,
                ["dispositivoid"] = dispositivoId
            };

            var response = await httpClient.PostAsync(
                "/connect/token",
                new FormUrlEncodedContent(formData),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<RespuestaToken>(json);
            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
                return null;

            await sesion.GuardaTokenAsync(tokenData.AccessToken, tokenData.RefreshToken);
            await sesion.GuardaExpiracionAsync(DateTime.Now.AddSeconds(tokenData.ExpiresIn));

            Logs.Info($"[AuthHandler] Refresh exitoso — expira en {tokenData.ExpiresIn}s");
            return tokenData.AccessToken;
        }
        catch (Exception ex)
        {
            Logs.Error($"[AuthHandler] Excepción en refresh: {ex.GetType().Name} - {ex.Message}");
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private Task ActivarModoOfflineAsync()
    {
        var access = Connectivity.Current.NetworkAccess;
        if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
        {
            var yaEraOffline = AppState.Instance.ModoOffline;
            AppState.Instance.ModoOffline = true;

            // Primera detección: si el usuario está en una página secundaria, volver al inicio
            if (!yaEraOffline)
            {
                Logs.Warn("[AuthHandler] Modo offline activado");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (AppState.Instance.ModoOffline &&
                        Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                        await Shell.Current.Navigation.PopToRootAsync(animated: false);
                });
            }
        }
        return Task.CompletedTask;
    }
}
