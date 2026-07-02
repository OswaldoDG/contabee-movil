using System.Net;

namespace ContaBeeMovil.Services;

// Motivo por el que el usuario perdió acceso a la cuenta fiscal (según el mensaje del 403).
public enum TipoAccesoPerdido
{
    Desconocido, // body vacío o no reconocido
    Desactivada, // "...la asociación... está inactiva" (reversible)
    Eliminada    // "el usuario no pertenece a la cuenta fiscal" (permanente)
}

// Códigos de error del backend que el front necesita reconocer explícitamente.
// (Espejo parcial del CodigosError.cs del backend.)
public static class CodigosErrorApi
{
    // Asociación usuario ↔ cuenta fiscal desactivada/eliminada por el primario (HTTP 403).
    // El usuario SIGUE autenticado; solo perdió acceso a esa cuenta fiscal.
    public const string AsociacionInactiva = "asociacion-inactiva";

    // En los endpoints de datos (transcript / facturación), un 403 significa siempre
    // "asociación inactiva". Es la señal MÁS robusta: el body a veces viene con el mensaje
    // ("La asociación del usuario con la cuenta fiscal está inactiva") y a veces vacío.
    public static bool EsAsociacionInactiva(HttpStatusCode? code, string? cuerpo)
        => code == HttpStatusCode.Forbidden || EsAsociacionInactiva(cuerpo);

    // Detección por texto del body, para cuando solo se tiene el cuerpo de la respuesta.
    public static bool EsAsociacionInactiva(string? cuerpo)
    {
        if (string.IsNullOrEmpty(cuerpo)) return false;
        return cuerpo.Contains(AsociacionInactiva, StringComparison.OrdinalIgnoreCase)                     // código
            || cuerpo.Contains("no pertenece", StringComparison.OrdinalIgnoreCase)                         // eliminada / legacy
            || cuerpo.Contains("inactiva", StringComparison.OrdinalIgnoreCase);                            // desactivada
    }

    // Distingue el motivo por el mensaje del backend (el body real, disponible en el
    // AuthHandler / coordinador aunque a la página a veces le llegue vacío).
    public static TipoAccesoPerdido ClasificarAccesoPerdido(string? cuerpo)
    {
        if (string.IsNullOrEmpty(cuerpo)) return TipoAccesoPerdido.Desconocido;
        if (cuerpo.Contains("inactiva", StringComparison.OrdinalIgnoreCase)) return TipoAccesoPerdido.Desactivada;
        if (cuerpo.Contains("no pertenece", StringComparison.OrdinalIgnoreCase)) return TipoAccesoPerdido.Eliminada;
        return TipoAccesoPerdido.Desconocido;
    }
}
