using System.Text.Json;
using Microsoft.Maui.Storage;

namespace ContaBeeMovil.Services.Almacenamiento;

public class ServicioAlmacenamiento : IServicioAlmacenamiento
{
    // ── SecureStorage ─────────────────────────────────────────────────────

    public async Task GuardarSeguroAsync(string clave, string valor)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("La clave no puede estar vacía.", nameof(clave));

        await SecureStorage.Default.SetAsync(clave, valor);
    }

    public async Task GuardarSeguroAsync<T>(string clave, T objeto)
    {
        var json = JsonSerializer.Serialize(objeto);
        await GuardarSeguroAsync(clave, json);
    }

    public async Task<string?> LeerSeguroAsync(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("La clave no puede estar vacía.", nameof(clave));

        return await SecureStorage.Default.GetAsync(clave);
    }

    public async Task<T?> LeerSeguroAsync<T>(string clave)
    {
        var valor = await LeerSeguroAsync(clave);
        if (valor == null) return default;

        var tipo = typeof(T);
        if (tipo == typeof(string))        return (T)(object)valor;
        if (tipo == typeof(bool)   && bool.TryParse(valor,   out var b)) return (T)(object)b;
        if (tipo == typeof(int)    && int.TryParse(valor,    out var i)) return (T)(object)i;
        if (tipo == typeof(double) && double.TryParse(valor, out var d)) return (T)(object)d;

        return JsonSerializer.Deserialize<T>(valor);
    }

    public async Task<bool> ExisteClaveSeguraAsync(string clave)
    {
        var valor = await SecureStorage.Default.GetAsync(clave);
        return valor != null;
    }

    public bool EliminarSeguro(string clave)
        => SecureStorage.Default.Remove(clave);

    public void LimpiarSeguro()
        => SecureStorage.Default.RemoveAll();

    // ── Preferences (SharedPreferences) ───────────────────────────────────

    public void GuardarPreferencia(string clave, string valor)
        => Preferences.Default.Set(clave, valor);

    public string LeerPreferencia(string clave, string valorPorDefecto = "")
        => Preferences.Default.Get(clave, valorPorDefecto);

    public void GuardarPreferencia(string clave, bool valor)
        => Preferences.Default.Set(clave, valor);

    public bool LeerPreferenciaBool(string clave, bool valorPorDefecto = false)
        => Preferences.Default.Get(clave, valorPorDefecto);

    public void GuardarPreferencia(string clave, int valor)
        => Preferences.Default.Set(clave, valor);

    public int LeerPreferenciaInt(string clave, int valorPorDefecto = 0)
        => Preferences.Default.Get(clave, valorPorDefecto);

    public void GuardarObjetoPreferencia<T>(string clave, T objeto)
    {
        var json = JsonSerializer.Serialize(objeto);
        Preferences.Default.Set(clave, json);
    }

    public T? LeerObjetoPreferencia<T>(string clave)
    {
        var json = Preferences.Default.Get<string?>(clave, null);
        if (json == null) return default;
        return JsonSerializer.Deserialize<T>(json);
    }

    public bool ContienePreferencia(string clave)
        => Preferences.Default.ContainsKey(clave);

    public void EliminarPreferencia(string clave)
        => Preferences.Default.Remove(clave);

    public void LimpiarPreferencias()
        => Preferences.Default.Clear();
}
