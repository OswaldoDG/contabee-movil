using ContaBeeMovil.Services.Dev;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace ContaBeeMovil.Services;

public interface IServicioActualizacion
{
    /// <summary>
    /// Consulta si hay una versión más nueva de la app y, de haberla,
    /// muestra el aviso "Ignorar / Actualizar" (u obligatorio si la versión instalada
    /// está por debajo de la mínima soportada). Fuente principal: el backend;
    /// si el endpoint aún no existe (404) cae a consultar la tienda directamente.
    /// Falla en silencio: sin red, timeout o respuesta inválida no molestan al usuario.
    /// </summary>
    Task VerificarAsync();
}

public class ServicioActualizacion(IHttpClientFactory factory, IServicioAlerta alerta, IServicioLogs logs) : IServicioActualizacion
{
    private const string URL_VERSION = "https://api.contabee.mx/api/identity/app/version-movil";
    private const string CLAVE_VERSION_IGNORADA = "Actualizacion_VersionIgnorada";

    // Fallbacks por si el backend no manda las URLs de tienda
    private const string URL_PLAY_STORE = "https://play.google.com/store/apps/details?id=mx.contabee.app";
    private const string URL_APP_STORE = "https://apps.apple.com/mx/app/contabee/id6761437536";
    private const string URL_ITUNES_LOOKUP = "https://itunes.apple.com/lookup?bundleId=mx.contabee.app&country=mx";

    public async Task VerificarAsync()
    {
        try
        {
            // Se dispara desde App.OnStart: dar tiempo a que la primera página
            // termine de montarse para que el popup tenga dónde mostrarse.
            await Task.Delay(3000);

            var access = Connectivity.Current.NetworkAccess;
            if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
                return;

            var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            // Fuente principal: nuestro backend. Mientras el endpoint no esté
            // deployado (404) se consulta la tienda directamente como fallback.
            var info = await ConsultarBackendAsync(client) ?? await ConsultarTiendaAsync(client);
            if (info is null)
                return;

            var instalada = AppInfo.Current.Version;
            Version.TryParse(info.VersionMinima, out var minima);
            Version.TryParse(info.VersionRecomendada, out var recomendada);

            var urlTienda = DeviceInfo.Platform == DevicePlatform.iOS
                ? (string.IsNullOrWhiteSpace(info.UrlIos) ? URL_APP_STORE : info.UrlIos)
                : (string.IsNullOrWhiteSpace(info.UrlAndroid) ? URL_PLAY_STORE : info.UrlAndroid);

            if (minima is not null && instalada < minima)
            {
                logs.Info($"Actualización obligatoria: instalada {instalada}, mínima {minima}");
                await MostrarAvisoAsync(obligatoria: true, info.Mensaje, urlTienda);
                return;
            }

            if (recomendada is null || instalada >= recomendada)
                return;

            // No repetir el aviso de una versión que el usuario ya decidió ignorar
            if (Preferences.Get(CLAVE_VERSION_IGNORADA, string.Empty) == recomendada.ToString())
                return;

            logs.Info($"Actualización disponible: instalada {instalada}, recomendada {recomendada}");
            var quiereActualizar = await MostrarAvisoAsync(obligatoria: false, info.Mensaje, urlTienda);
            if (!quiereActualizar)
                Preferences.Set(CLAVE_VERSION_IGNORADA, recomendada.ToString());
        }
        catch (Exception ex)
        {
            // El chequeo de versión nunca debe interrumpir el arranque de la app
            logs.Warn($"Chequeo de versión falló: {ex.Message}");
        }
    }

    private async Task<VersionMovilRespuesta?> ConsultarBackendAsync(HttpClient client)
    {
        try
        {
            using var res = await client.GetAsync(URL_VERSION);
            if (!res.IsSuccessStatusCode)
                return null; // 404 mientras el endpoint no esté deployado

            var json = await res.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<VersionMovilRespuesta>(json);
        }
        catch
        {
            return null;
        }
    }

    // ── Fallback: consultar la tienda directamente ─────────────────────────
    // Solo produce VersionRecomendada (la tienda no sabe de versión mínima,
    // así que por esta vía nunca hay aviso obligatorio). Cuando el backend
    // exponga su endpoint, éste toma prioridad y el fallback deja de usarse.

    private async Task<VersionMovilRespuesta?> ConsultarTiendaAsync(HttpClient client)
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS)
                return await ConsultarAppStoreAsync(client);
            if (DeviceInfo.Platform == DevicePlatform.Android)
                return await ConsultarPlayStoreAsync(client);
            return null;
        }
        catch (Exception ex)
        {
            logs.Warn($"Consulta de versión en tienda falló: {ex.Message}");
            return null;
        }
    }

    private static async Task<VersionMovilRespuesta?> ConsultarAppStoreAsync(HttpClient client)
    {
        var json = await client.GetStringAsync(URL_ITUNES_LOOKUP);
        var respuesta = JsonConvert.DeserializeObject<RespuestaItunes>(json);
        var app = respuesta?.Results?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(app?.Version))
            return null;

        return new VersionMovilRespuesta
        {
            VersionRecomendada = app.Version,
            UrlIos = app.TrackViewUrl,
        };
    }

    private static async Task<VersionMovilRespuesta?> ConsultarPlayStoreAsync(HttpClient client)
    {
        // Play Store no tiene API pública de consulta; la versión viene embebida
        // en la página web como [[["2.0.14"]]]. Si Google cambia el formato el
        // regex deja de coincidir y simplemente no hay aviso por esta vía.
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{URL_PLAY_STORE}&hl=es");
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        using var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            return null;

        var html = await res.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"\[\[\[""(\d+(?:\.\d+)+)""\]\]");
        if (!match.Success)
            return null;

        return new VersionMovilRespuesta { VersionRecomendada = match.Groups[1].Value };
    }

    private async Task<bool> MostrarAvisoAsync(bool obligatoria, string? mensajeServidor, string urlTienda)
    {
        var titulo = obligatoria ? "Actualización requerida" : "Actualización disponible";
        var mensaje = !string.IsNullOrWhiteSpace(mensajeServidor)
            ? mensajeServidor
            : obligatoria
                ? "Tu versión de ContaBee ya no es compatible. Actualiza desde la tienda para continuar."
                : "Hay una nueva versión de ContaBee disponible en la tienda.";

        var confirmado = await MainThread.InvokeOnMainThreadAsync(() =>
            alerta.MostrarAsync(
                titulo,
                mensaje,
                verBotonCancelar: !obligatoria,
                cancelarText: "Ignorar",
                confirmarText: "Actualizar"));

        if (confirmado)
            await Launcher.Default.OpenAsync(urlTienda);

        return confirmado;
    }

    private sealed class RespuestaItunes
    {
        public List<ResultadoItunes>? Results { get; set; }
    }

    private sealed class ResultadoItunes
    {
        public string? Version { get; set; }
        public string? TrackViewUrl { get; set; }
    }
}

/// <summary>
/// Respuesta de GET api/identity/app/version-movil.
/// </summary>
public class VersionMovilRespuesta
{
    /// <summary>Versiones menores a ésta muestran aviso obligatorio (sin botón de ignorar).</summary>
    public string? VersionMinima { get; set; }

    /// <summary>Versiones menores a ésta muestran aviso con "Ignorar / Actualizar".</summary>
    public string? VersionRecomendada { get; set; }

    public string? UrlAndroid { get; set; }
    public string? UrlIos { get; set; }

    /// <summary>Texto opcional del aviso definido desde el servidor; si viene vacío se usa el default.</summary>
    public string? Mensaje { get; set; }
}
