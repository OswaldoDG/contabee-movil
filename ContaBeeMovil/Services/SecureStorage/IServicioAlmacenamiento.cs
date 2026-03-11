namespace ContaBeeMovil.Services.Almacenamiento;

/// <summary>
/// Servicio unificado de almacenamiento.
/// - SecureStorage: datos sensibles (tokens, credenciales).
/// - Preferences / SharedPreferences: datos no sensibles (configuración, flags).
/// </summary>
public interface IServicioAlmacenamiento
{
    // ── SecureStorage ─────────────────────────────────────────────────────

    Task GuardarSeguroAsync(string clave, string valor);
    Task GuardarSeguroAsync<T>(string clave, T objeto);

    Task<string?> LeerSeguroAsync(string clave);
    Task<T?> LeerSeguroAsync<T>(string clave);

    Task<bool> ExisteClaveSeguraAsync(string clave);
    bool EliminarSeguro(string clave);
    void LimpiarSeguro();

    // ── Preferences (SharedPreferences) ───────────────────────────────────

    void GuardarPreferencia(string clave, string valor);
    string LeerPreferencia(string clave, string valorPorDefecto = "");

    void GuardarPreferencia(string clave, bool valor);
    bool LeerPreferenciaBool(string clave, bool valorPorDefecto = false);

    void GuardarPreferencia(string clave, int valor);
    int LeerPreferenciaInt(string clave, int valorPorDefecto = 0);

    void GuardarObjetoPreferencia<T>(string clave, T objeto);
    T? LeerObjetoPreferencia<T>(string clave);

    bool ContienePreferencia(string clave);
    void EliminarPreferencia(string clave);
    void LimpiarPreferencias();
}
