using System.Net.Http.Headers;
using System.Text.Json;
using Contabee.Api.Identidad;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
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

    private readonly IServiceProvider _serviceProvider;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IServicioLogs? _logs;
    private IServicioLogs Logs => _logs ??= _serviceProvider.GetRequiredService<IServicioLogs>();

    public AuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        bool esPublica = (_rutasPublicas.Any(r => path.StartsWith(r)) && !path.EndsWith("/vincular") && !path.EndsWith("/vinculado"))
            || (request.Method == HttpMethod.Get && _rutasPublicasGet.Any(r => path.StartsWith(r)));

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
                    Logs.Warn("[AuthHandler] Refresh falló — cerrando sesión");
                    await CerrarSesionAsync(sesion, appState);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                }
            }
            else
            {
                Logs.Info("[AuthHandler] Token expirado sin posibilidad de refresh — cerrando sesión");
                await CerrarSesionAsync(sesion, appState);
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

                    if (body.Contains("no pertenece a la cuenta fiscal", StringComparison.OrdinalIgnoreCase))
                    {
                        Logs.Warn($"[AuthHandler] Desvinculación detectada — {path}");
                        _ = sesion.ManejarDesvinculacionAsync();
                    }
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

    private async Task CerrarSesionAsync(IServicioSesion sesion, AppState appState)
    {
        Logs.Warn("[AuthHandler] Sesión cerrada por token inválido");
        await sesion.LimpiaTokensAsync();
        appState.Perfil = null;
        appState.CuentasFiscales = null;
        appState.CuentaFiscalActual = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var toast = _serviceProvider.GetRequiredService<IServicioToast>();
            var paginaLogin = _serviceProvider.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = new NavigationPage(paginaLogin);
            await toast.MostrarAsync("Tu sesión ha caducado", ToastIcono.Warning, ToastPosicion.Bottom);
        });
    }
}
