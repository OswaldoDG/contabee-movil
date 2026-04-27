using System.Net.Http.Headers;
using System.Text.Json;
using Contabee.Api.Identidad;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Pages.SinConexion;
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
        "/connect/token"
    ];

    private readonly IServiceProvider _serviceProvider;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        bool esPublica = _rutasPublicas.Any(r => path.StartsWith(r));

        if (esPublica)
            return await base.SendAsync(request, cancellationToken);

        // Verificar conectividad antes de cualquier llamada autenticada
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await MostrarPaginaSinConexionAsync();
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
            bool puedeRefrescar = appState.Recordarme && !string.IsNullOrEmpty(refreshToken);

            if (puedeRefrescar)
            {
                var nuevoToken = await RefrescarTokenAsync(sesion, refreshToken!, cancellationToken);
                if (nuevoToken != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nuevoToken);
                }
                else
                {
                    await CerrarSesionAsync(sesion, appState);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                }
            }
            else
            {
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
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException) when (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            // La red se cayó entre el check inicial y la petición efectiva
            await MostrarPaginaSinConexionAsync();
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

            return tokenData.AccessToken;
        }
        catch
        {
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task MostrarPaginaSinConexionAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var pagina = _serviceProvider.GetRequiredService<PaginaSinConexion>();
            Application.Current!.Windows[0].Page = pagina;
        });
    }

    private async Task CerrarSesionAsync(IServicioSesion sesion, AppState appState)
    {
        await sesion.LimpiaTokensAsync();
        appState.Perfil = null;
        appState.CuentasFiscales = null;
        appState.CuentaFiscalActual = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var toast = _serviceProvider.GetRequiredService<IServicioToast>();
            var paginaLogin = _serviceProvider.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = paginaLogin;
            await toast.MostrarAsync("Tu sesión ha caducado", ToastIcono.Warning, ToastPosicion.Bottom);
        });
    }
}
