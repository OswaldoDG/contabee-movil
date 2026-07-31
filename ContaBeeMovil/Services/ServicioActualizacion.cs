using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Pages.SinConexion;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Views;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace ContaBeeMovil.Services;

public interface IServicioActualizacion
{
    /// <summary>
    /// Consulta al backend si la versión instalada quedó atrás y, de ser así,
    /// muestra el aviso "Ignorar / Actualizar". El aviso es informativo, nunca
    /// bloqueante. Falla en silencio: sin red, timeout, 400 o respuesta inválida
    /// no molestan al usuario ni interrumpen el arranque.
    /// </summary>
    /// <param name="esArranque">
    /// true = arranque frío (espera al primer render antes de mostrar el aviso).
    /// false = reanudación (App.OnResume): sin espera inicial.
    /// </param>
    Task VerificarAsync(bool esArranque = true);
}

public class ServicioActualizacion(IHttpClientFactory factory, IServicioLogs logs) : IServicioActualizacion
{
    // ── Parámetros ajustables ────────────────────────────────────────────────
    /// <summary>Throttle: cada cuánto, como mínimo, se vuelve a consultar la versión.</summary>
    private static readonly TimeSpan INTERVALO_ENTRE_CHEQUEOS = TimeSpan.FromDays(1);

    /// <summary>Cuánto se respeta el "Ignorar" antes de volver a avisar la MISMA versión.</summary>
    private static readonly TimeSpan PERIODO_REMOSTRAR_IGNORADA = TimeSpan.FromDays(3);
    // ─────────────────────────────────────────────────────────────────────────

    private const string URL_VERIFICA = "https://api.contabee.mx/api/identity/app/version-movil/verifica";
    private const string CLAVE_VERSION_IGNORADA = "Actualizacion_VersionIgnorada";
    private const string CLAVE_FECHA_IGNORADA = "Actualizacion_FechaIgnorada";
    private const string CLAVE_ULTIMO_CHEQUEO = "Actualizacion_UltimoChequeo";

    // Serializa las verificaciones: solo una a la vez. Como el semáforo se
    // mantiene tomado durante todo el aviso, mientras un popup está visible
    // cualquier otra reanudación que dispare VerificarAsync sale de inmediato
    // (no se apilan popups).
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Fallbacks por si el backend no manda la URL de tienda
    private const string URL_PLAY_STORE = "https://play.google.com/store/apps/details?id=mx.contabee.app";
    private const string URL_APP_STORE = "https://apps.apple.com/mx/app/contabee/id6761437536";
    private const string URL_ITUNES_LOOKUP = "https://itunes.apple.com/lookup?bundleId=mx.contabee.app&country=mx";

    public async Task VerificarAsync(bool esArranque = true)
    {
        try
        {
            // En arranque frío, dar tiempo a que la primera página termine de
            // montarse para que el popup tenga dónde mostrarse. En reanudación
            // la UI ya está lista, así que no se espera.
            if (esArranque)
                await Task.Delay(3000);

            // Solo una verificación a la vez. Si ya hay una corriendo (incluido
            // el tiempo que un popup está visible), esta reanudación se descarta.
            if (!await _gate.WaitAsync(0))
                return;

            try
            {
                var access = Connectivity.Current.NetworkAccess;
                if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
                    return;

                // Throttle: no reconsultar si el último chequeo fue hace menos de
                // INTERVALO_ENTRE_CHEQUEOS (persiste entre arranques).
                var ultimoChequeo = Preferences.Get(CLAVE_ULTIMO_CHEQUEO, DateTime.MinValue);
                if (DateTime.UtcNow - ultimoChequeo < INTERVALO_ENTRE_CHEQUEOS)
                    return;

                var info = await ConsultarAsync();
                if (info is null)
                    return;

                // Consulta exitosa: registrar para el throttle.
                Preferences.Set(CLAVE_ULTIMO_CHEQUEO, DateTime.UtcNow);

                if (!info.RequiereActualizacion)
                    return;

                // Versión ya ignorada: no repetir el aviso mientras siga dentro del
                // plazo PERIODO_REMOSTRAR_IGNORADA. Pasado ese plazo, vuelve a avisar.
                var versionServidor = info.VersionActual ?? string.Empty;
                if (versionServidor.Length > 0 &&
                    Preferences.Get(CLAVE_VERSION_IGNORADA, string.Empty) == versionServidor)
                {
                    var fechaIgnorada = Preferences.Get(CLAVE_FECHA_IGNORADA, DateTime.MinValue);
                    if (DateTime.UtcNow - fechaIgnorada < PERIODO_REMOSTRAR_IGNORADA)
                        return;
                }

                logs.Info($"Actualización disponible: instalada {VersionInstalada()}, vigente {versionServidor}");
                var respuesta = await MostrarAvisoAsync(
                    info.Mensaje, info.VersionActual, UrlTienda(info.UrlActualizacion));

                // Solo un "Ahora no" explícito silencia esta versión; cerrar tocando
                // fuera no cuenta (el throttle diario ya evita que insista hoy).
                if (respuesta == RespuestaAviso.Pospuesto && versionServidor.Length > 0)
                {
                    Preferences.Set(CLAVE_VERSION_IGNORADA, versionServidor);
                    Preferences.Set(CLAVE_FECHA_IGNORADA, DateTime.UtcNow);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            // El chequeo de versión nunca debe interrumpir el arranque de la app
            logs.Warn($"Chequeo de versión falló: {ex.Message}");
        }
    }

    // ── Consulta al backend ──────────────────────────────────────────────────

    private async Task<ResultadoVersion?> ConsultarAsync()
    {
        var plataforma = PlataformaBackend();
        if (plataforma is null)
            return null; // Windows / MacCatalyst: el backend solo conoce Android e Ios

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        var backend = await ConsultarBackendAsync(client, plataforma);
        if (backend is not null)
            return backend;

        // El backend no contestó (sin red, 5xx, endpoint caído): último recurso,
        // preguntarle a la tienda. Un 400 NO llega aquí, ver ConsultarBackendAsync.
        return await ConsultarTiendaAsync(client);
    }

    private async Task<ResultadoVersion?> ConsultarBackendAsync(HttpClient client, string plataforma)
    {
        try
        {
            var version = VersionParaBackend();
            var url = $"{URL_VERIFICA}?version={Uri.EscapeDataString(version)}&plataforma={plataforma}";

            using var res = await client.GetAsync(url);

            // 400 = versión no parseable, plataforma faltante, o build interno por
            // delante de la publicada (TestFlight / internal testing). Ninguno es
            // motivo para avisar: se responde "no actualizar" y se sigue de largo.
            if (res.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var detalle = await res.Content.ReadAsStringAsync();
                logs.Info($"Chequeo de versión ({version}/{plataforma}) devolvió 400: {detalle}");
                return new ResultadoVersion { RequiereActualizacion = false };
            }

            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync();
            var respuesta = JsonConvert.DeserializeObject<RespuestaVerificacion>(json);
            if (respuesta is null)
                return null;

            return new ResultadoVersion
            {
                RequiereActualizacion = respuesta.RequiereActualizacion,
                VersionActual = respuesta.VersionActual,
                UrlActualizacion = respuesta.UrlActualizacion,
                Mensaje = respuesta.Mensaje,
            };
        }
        catch (Exception ex)
        {
            logs.Warn($"Chequeo de versión en backend falló: {ex.Message}");
            return null;
        }
    }

    /// <summary>Android | Ios; null en plataformas que el backend no contempla.</summary>
    private static string? PlataformaBackend()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android) return "Android";
        if (DeviceInfo.Platform == DevicePlatform.iOS) return "Ios";
        return null;
    }

    private static Version VersionInstalada() => AppInfo.Current.Version;

    /// <summary>
    /// El backend espera mayor.menor.parche. AppInfo puede traer 4 componentes
    /// (1.0.38.0), así que se recorta; el sufijo +build no aplica en MAUI.
    /// </summary>
    private static string VersionParaBackend()
    {
        var v = VersionInstalada();
        return $"{Math.Max(v.Major, 0)}.{Math.Max(v.Minor, 0)}.{Math.Max(v.Build, 0)}";
    }

    private static string UrlTienda(string? urlBackend) =>
        !string.IsNullOrWhiteSpace(urlBackend)
            ? urlBackend
            : DeviceInfo.Platform == DevicePlatform.iOS ? URL_APP_STORE : URL_PLAY_STORE;

    // ── Fallback: consultar la tienda directamente ─────────────────────────
    // Solo se usa cuando el backend no está disponible. La tienda no manda
    // mensaje configurable, así que el aviso sale con el texto por defecto.

    private async Task<ResultadoVersion?> ConsultarTiendaAsync(HttpClient client)
    {
        try
        {
            var enTienda = DeviceInfo.Platform == DevicePlatform.iOS
                ? await ConsultarAppStoreAsync(client)
                : await ConsultarPlayStoreAsync(client);

            if (enTienda is null || !Version.TryParse(enTienda.Version, out var publicada))
                return null;

            return new ResultadoVersion
            {
                RequiereActualizacion = VersionInstalada() < publicada,
                VersionActual = publicada.ToString(),
                UrlActualizacion = enTienda.Url,
            };
        }
        catch (Exception ex)
        {
            logs.Warn($"Consulta de versión en tienda falló: {ex.Message}");
            return null;
        }
    }

    private static async Task<VersionEnTienda?> ConsultarAppStoreAsync(HttpClient client)
    {
        var json = await client.GetStringAsync(URL_ITUNES_LOOKUP);
        var respuesta = JsonConvert.DeserializeObject<RespuestaItunes>(json);
        var app = respuesta?.Results?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(app?.Version))
            return null;

        return new VersionEnTienda { Version = app.Version, Url = app.TrackViewUrl };
    }

    private static async Task<VersionEnTienda?> ConsultarPlayStoreAsync(HttpClient client)
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

        return new VersionEnTienda { Version = match.Groups[1].Value, Url = URL_PLAY_STORE };
    }

    // ── Aviso ────────────────────────────────────────────────────────────────

    private enum RespuestaAviso
    {
        /// <summary>No se pudo mostrar el aviso, o el usuario lo cerró tocando fuera del popup.</summary>
        Descartado,
        /// <summary>Tocó "Ahora no": es la única respuesta que silencia esta versión por un tiempo.</summary>
        Pospuesto,
        /// <summary>Tocó "Actualizar": se abre la tienda.</summary>
        Actualizar,
    }

    private async Task<RespuestaAviso> MostrarAvisoAsync(string? mensajeServidor, string? versionNueva, string urlTienda)
    {
        var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;

        // Sin página donde montar el popup, o con la pantalla "Sin conexión"
        // encima, el aviso se omite: no hay dónde mostrarlo o estorbaría.
        if (pagina is null or PaginaSinConexion)
            return RespuestaAviso.Descartado;

        var respuesta = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var popup = new ActualizacionPopup(mensajeServidor, VersionParaBackend(), versionNueva);
            await pagina.ShowPopupAsync(popup);

            if (popup.Confirmado) return RespuestaAviso.Actualizar;
            return popup.Pospuesto ? RespuestaAviso.Pospuesto : RespuestaAviso.Descartado;
        });

        if (respuesta == RespuestaAviso.Actualizar)
            await Launcher.Default.OpenAsync(urlTienda);

        return respuesta;
    }

    // ── DTOs internos ────────────────────────────────────────────────────────

    private sealed class ResultadoVersion
    {
        public bool RequiereActualizacion { get; init; }
        public string? VersionActual { get; init; }
        public string? UrlActualizacion { get; init; }
        public string? Mensaje { get; init; }
    }

    private sealed class VersionEnTienda
    {
        public string? Version { get; init; }
        public string? Url { get; init; }
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

    /// <summary>
    /// Respuesta de GET api/identity/app/version-movil/verifica?version=&amp;plataforma=
    /// (nombre distinto al DTO generado por NSwag para no chocar con él).
    /// </summary>
    private sealed class RespuestaVerificacion
    {
        /// <summary>Android | Ios — eco de la plataforma enviada.</summary>
        public string? Plataforma { get; set; }

        /// <summary>Eco de la versión enviada; útil solo para depurar.</summary>
        public string? VersionRecibida { get; set; }

        /// <summary>Versión vigente según el backend.</summary>
        public string? VersionActual { get; set; }

        /// <summary>Único campo que decide si se muestra el aviso.</summary>
        public bool RequiereActualizacion { get; set; }

        /// <summary>Play Store o App Store según la plataforma enviada.</summary>
        public string? UrlActualizacion { get; set; }

        /// <summary>Texto configurable del aviso; null cuando no aplica o no está configurado.</summary>
        public string? Mensaje { get; set; }
    }
}
