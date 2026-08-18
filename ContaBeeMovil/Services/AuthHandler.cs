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
    private InterruptorApi? _interruptor;
    private InterruptorApi Interruptor => _interruptor ??= _serviceProvider.GetRequiredService<InterruptorApi>();

    // Esperas entre intentos de refresh. El primer intento es inmediato; los dos siguientes
    // absorben los blips cortos de gateway sin castigar al usuario con un error.
    private static readonly TimeSpan[] _esperasReintento =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3)
    ];

    public AuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // CONTRATO DE STATUS CODES SINTÉTICOS
    //
    // Aguas abajo (ServicioSesion.MotivoPorHttp y los catch de las páginas) lo único que
    // sobrevive de esta respuesta es el status code: NSwag descarta headers y body. Así que
    // el código ES el mensaje, y solo hay dos significados posibles:
    //
    //   401 → la sesión está muerta con certeza. Quien lo reciba puede cerrar sesión.
    //   503 → no sabemos / es culpa de la infraestructura. NADIE debe tocar los tokens.
    //
    // Regla: ningún fallo de red, gateway o backend puede salir de aquí como 401.
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

        // Circuito abierto: el backend ya demostró estar caído. Fallamos al instante en vez
        // de sumar otra llamada a la tormenta y congelar la UI durante todo el timeout.
        if (!Interruptor.PermitirLlamada())
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);

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
                var refresh = await RefrescarTokenAsync(sesion, refreshToken!, cancellationToken);

                switch (refresh.Estado)
                {
                    case EstadoRefresh.Exito:
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refresh.Token);
                        break;

                    case EstadoRefresh.Rechazado:
                        // El servidor de identidad dijo explícitamente que el refresh token ya
                        // no sirve (invalid_grant) → token muerto, esta sí es una expiración real.
                        Logs.Warn($"[AuthHandler] Refresh rechazado por el servidor ({refresh.Detalle}) — token revocado");
                        await Coordinador.CerrarSesionAsync(MotivoCierre.TokenRevocado);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);

                    default:
                        // Fallo de infraestructura (5xx, timeout, gateway caído, body ilegible).
                        // NO sabemos si el token sigue vivo, así que NO lo tocamos: la sesión se
                        // conserva intacta y la llamada se reporta como servicio no disponible.
                        Logs.Warn($"[AuthHandler] Refresh no concluyente ({refresh.Detalle}) — se conserva la sesión");
                        RegistrarFalloInfraestructura($"refresh: {refresh.Detalle}");
                        return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
                }
            }
            else
            {
                // Loginless sin refresh token (instalación legacy anterior al fix de
                // offline_access): NO borrar el token loginless — es recuperable. Diferimos
                // a la reanudación (OnResume/arranque), que re-loguea con el token loginless
                // y sana la sesión con un refresh token nuevo.
                //
                // Devolvemos 503 y NO 401 a propósito: aguas abajo solo sobrevive el status
                // code (NSwag descarta headers), y MotivoPorHttp traduce cualquier 401 en
                // TokenRevocado → CerrarSesionAsync borraría el token loginless, justo lo
                // contrario de lo que este bloque quiere. 503 = "no concluyente, no toques
                // la sesión", que es exactamente el caso.
                if (esLoginLess)
                {
                    Logs.Warn("[AuthHandler] Loginless sin refresh — se conserva token; sanará al reanudar");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
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

            // Un 5xx es el backend caído; cualquier otro código (incluidos 400/403/404) es una
            // respuesta legítima y por tanto prueba de que el servicio está vivo.
            if ((int)response.StatusCode >= 500)
                RegistrarFalloInfraestructura($"HTTP {(int)response.StatusCode} en {path}");
            else
                RegistrarServicioVivo();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    response.Content = new StringContent(
                        body,
                        System.Text.Encoding.UTF8,
                        response.Content.Headers.ContentType?.MediaType ?? "application/json");

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Hay red pero la petición no llegó a completarse: DNS, TLS, socket o timeout.
            // Es el backend, no el dispositivo — y jamás debe leerse como sesión inválida.
            Logs.Warn($"[AuthHandler] {ex.GetType().Name} en {path} — servicio no disponible");
            RegistrarFalloInfraestructura(ex.GetType().Name);
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }

    // Contabiliza un fallo atribuible al backend y enciende el aviso de servicio caído.
    private void RegistrarFalloInfraestructura(string detalle)
    {
        if (Interruptor.RegistrarFallo())
            Logs.Error($"[AuthHandler] Interruptor ABIERTO tras fallos consecutivos — último: {detalle}");

        if (!AppState.Instance.ServicioNoDisponible)
            MainThread.BeginInvokeOnMainThread(() => AppState.Instance.ServicioNoDisponible = true);
    }

    // El backend contestó: cierra el interruptor y apaga el aviso.
    private void RegistrarServicioVivo()
    {
        Interruptor.RegistrarExito();

        if (AppState.Instance.ServicioNoDisponible)
            MainThread.BeginInvokeOnMainThread(() => AppState.Instance.ServicioNoDisponible = false);
    }

    // Desenlace de un intento de refresh. Distinguir "el servidor rechazó el token" de "el
    // servidor no contestó" es crítico: solo lo primero justifica cerrar la sesión. Cuando
    // ambos casos colapsaban en null, una caída del API deslogueaba a todo el mundo.
    private enum EstadoRefresh
    {
        Exito,       // token nuevo en mano
        Rechazado,   // el servidor respondió invalid_grant → el refresh token está muerto
        Transitorio  // 5xx / timeout / red / body ilegible → no sabemos nada, no tocar tokens
    }

    private readonly record struct ResultadoRefresh(EstadoRefresh Estado, string? Token, string Detalle)
    {
        public static ResultadoRefresh Ok(string token)          => new(EstadoRefresh.Exito, token, "ok");
        public static ResultadoRefresh Rechazo(string detalle)   => new(EstadoRefresh.Rechazado, null, detalle);
        public static ResultadoRefresh Transitorio(string detalle) => new(EstadoRefresh.Transitorio, null, detalle);
    }

    private async Task<ResultadoRefresh> RefrescarTokenAsync(
        IServicioSesion sesion, string refreshToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after lock: another concurrent request may have already refreshed
            var expiracion = await sesion.LeeExpiracionAsync();
            if (expiracion.HasValue && DateTime.Now < expiracion.Value)
            {
                var vigente = await sesion.LeeAccessTokenAsync();
                if (!string.IsNullOrEmpty(vigente))
                    return ResultadoRefresh.Ok(vigente);
            }

            var resultado = ResultadoRefresh.Transitorio("sin intentos");
            bool huboIntentoIncierto = false;

            for (int i = 0; i < _esperasReintento.Length; i++)
            {
                if (_esperasReintento[i] > TimeSpan.Zero)
                    await Task.Delay(_esperasReintento[i], cancellationToken);

                resultado = await IntentarRefrescarAsync(sesion, refreshToken, cancellationToken);

                if (resultado.Estado == EstadoRefresh.Exito)
                    return resultado;

                if (resultado.Estado == EstadoRefresh.Rechazado)
                {
                    // Trampa de la rotación de refresh tokens: si un intento anterior murió sin
                    // respuesta, el servidor pudo haberlo procesado igual y rotado el token. En
                    // ese caso este invalid_grant no prueba que la sesión esté muerta, solo que
                    // reenviamos un token ya consumido — y creerle costaría un logout indebido.
                    // Lo degradamos a transitorio; si de verdad está muerto, el siguiente ciclo
                    // (sin intentos inciertos previos) lo dirá al primer intento.
                    if (huboIntentoIncierto)
                    {
                        Logs.Warn("[AuthHandler] invalid_grant tras un intento sin respuesta — puede ser rotación, no se cierra sesión");
                        return ResultadoRefresh.Transitorio("invalid_grant dudoso por rotación");
                    }

                    return resultado;
                }

                huboIntentoIncierto = true;
                Logs.Warn($"[AuthHandler] Refresh intento {i + 1}/{_esperasReintento.Length} falló ({resultado.Detalle})");
            }

            return resultado;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<ResultadoRefresh> IntentarRefrescarAsync(
        IServicioSesion sesion, string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
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

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return ClasificarFallo(response.StatusCode, json);

            RespuestaToken? tokenData;
            try
            {
                tokenData = JsonSerializer.Deserialize<RespuestaToken>(json);
            }
            catch (JsonException)
            {
                // 200 con body que no es el JSON esperado: típico de un proxy o portal
                // cautivo intermediando. No es un rechazo del token.
                return ResultadoRefresh.Transitorio("200 con body no-JSON");
            }

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
                return ResultadoRefresh.Transitorio("200 sin access_token");

            await sesion.GuardaTokenAsync(tokenData.AccessToken, tokenData.RefreshToken);
            await sesion.GuardaExpiracionAsync(DateTime.Now.AddSeconds(tokenData.ExpiresIn));

            Logs.Info($"[AuthHandler] Refresh exitoso — expira en {tokenData.ExpiresIn}s");
            return ResultadoRefresh.Ok(tokenData.AccessToken);
        }
        catch (Exception ex)
        {
            // Timeout, DNS, socket, TLS... nada de esto dice que el token esté muerto.
            Logs.Error($"[AuthHandler] Excepción en refresh: {ex.GetType().Name} - {ex.Message}");
            return ResultadoRefresh.Transitorio(ex.GetType().Name);
        }
    }

    // Un refresh fallido solo es definitivo cuando el servidor de identidad lo dice con el
    // vocabulario de OAuth2 (RFC 6749 §5.2): 400 + JSON con error=invalid_grant. Todo lo
    // demás —5xx del gateway, HTML de nginx, 429, 401 de client auth— es infraestructura,
    // y ante infraestructura la política es conservar la sesión.
    private static ResultadoRefresh ClasificarFallo(System.Net.HttpStatusCode code, string body)
    {
        if (code != System.Net.HttpStatusCode.BadRequest)
            return ResultadoRefresh.Transitorio($"HTTP {(int)code}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                var codigo = error.GetString();
                return codigo is "invalid_grant" or "invalid_token"
                    ? ResultadoRefresh.Rechazo(codigo)
                    : ResultadoRefresh.Transitorio($"400 {codigo}");
            }
        }
        catch (JsonException) { }

        // 400 sin JSON OAuth interpretable (p.ej. página de error del gateway) → no concluyente.
        return ResultadoRefresh.Transitorio("400 sin error OAuth");
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
