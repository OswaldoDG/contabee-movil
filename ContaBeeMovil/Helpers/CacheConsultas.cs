using System.Security.Cryptography;
using System.Text;
using ContaBeeMovil.Services.Device;
using Newtonsoft.Json;

namespace ContaBeeMovil.Helpers;

/// <summary>
/// Caché persistente (Preferences) de respuestas del API. Permite pintar al instante lo último
/// conocido mientras se consulta en segundo plano: los pods de Azure se apagan sin tráfico y la
/// primera respuesta tras un arranque en frío puede tardar mucho.
/// Las claves se registran en un índice para poder podarlas y borrarlas al cerrar sesión
/// (Preferences no permite enumerar claves).
/// </summary>
public static class CacheConsultas
{
    public sealed record Entrada<T>(T Datos, DateTime GuardadoUtc);

    /// <summary>
    /// Tiempo mínimo que se muestra el esqueleto aunque los datos salgan de caché. Sin él, al
    /// tocar "Buscar" la lista se queda igual al instante y parece que la app no respondió.
    /// </summary>
    public static readonly TimeSpan PausaVisual = TimeSpan.FromMilliseconds(400);

    private const string Prefijo     = "Cache_";
    private const string ClaveIndice = "Cache__Indice";
    // Al pasar del máximo se descartan las entradas más viejas (FIFO por fecha de alta).
    private const int MaximoEntradas = 60;

    private static readonly object _lock = new();

    /// <summary>
    /// Clave estable para una pantalla, cuenta fiscal y consulta. <paramref name="consulta"/> se
    /// serializa a JSON y se resume con SHA-256, así que dos búsquedas con los mismos filtros,
    /// orden y página comparten entrada.
    /// </summary>
    public static string Clave(string pantalla, Guid cuentaFiscalId, object consulta)
    {
        var json = consulta as string ?? JsonConvert.SerializeObject(consulta);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
        return $"{Prefijo}{pantalla}_{cuentaFiscalId:N}_{hash}";
    }

    public static Entrada<T>? Leer<T>(string clave)
    {
        var json = Preferences.Default.Get(clave, string.Empty);
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonConvert.DeserializeObject<Entrada<T>>(json);
        }
        catch (JsonException)
        {
            // Formato viejo o corrupto: se descarta y la pantalla consulta como si no hubiera caché.
            Preferences.Default.Remove(clave);
            return null;
        }
    }

    public static void Guardar<T>(string clave, T datos)
    {
        string json;
        try
        {
            json = JsonConvert.SerializeObject(new Entrada<T>(datos, DateTime.UtcNow));
        }
        catch (JsonException)
        {
            return; // la caché es una optimización: si no se puede guardar, la pantalla sigue igual
        }

        lock (_lock)
        {
            Preferences.Default.Set(clave, json);

            var indice = LeerIndice();
            if (indice.Contains(clave)) return;

            indice.Add(clave);
            while (indice.Count > MaximoEntradas)
            {
                Preferences.Default.Remove(indice[0]);
                indice.RemoveAt(0);
            }
            Preferences.Default.Set(ClaveIndice, JsonConvert.SerializeObject(indice));
        }
    }

    // Pantallas que cachean: primer segmento de la clave, usado también para invalidar.
    public const string Dashboard      = "Dashboard";
    public const string Facturacion    = "Facturacion";
    public const string Devoluciones   = "Devoluciones";
    public const string Comprobaciones = "Comprobaciones";
    public const string Tienda         = "Tienda";   // catálogo, no depende de la cuenta (Guid.Empty)

    /// <summary>
    /// Borra todo lo cacheado de una pantalla para la cuenta fiscal actual (todos los filtros).
    /// Se llama tras una acción que modifica esos datos (crear, editar, eliminar, enviar
    /// captura): al regresar, la pantalla consulta en vez de mostrar el estado anterior. La
    /// acción acaba de despertar el API, así que esa consulta responde rápido.
    /// </summary>
    public static void Invalidar(string pantalla)
    {
        var cfid = AppState.Instance.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid is not Guid id) return;

        var prefijo = $"{Prefijo}{pantalla}_{id:N}_";
        lock (_lock)
        {
            var indice = LeerIndice();
            var borradas = indice.RemoveAll(clave =>
            {
                if (!clave.StartsWith(prefijo, StringComparison.Ordinal)) return false;
                Preferences.Default.Remove(clave);
                return true;
            });
            if (borradas > 0)
                Preferences.Default.Set(ClaveIndice, JsonConvert.SerializeObject(indice));
        }
    }

    /// <summary>Borra toda la caché. Se llama al cerrar sesión: son datos fiscales del usuario.</summary>
    public static void Limpiar()
    {
        lock (_lock)
        {
            foreach (var clave in LeerIndice())
                Preferences.Default.Remove(clave);
            Preferences.Default.Remove(ClaveIndice);
        }
    }

    private static List<string> LeerIndice()
    {
        var json = Preferences.Default.Get(ClaveIndice, string.Empty);
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonConvert.DeserializeObject<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
