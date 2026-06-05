namespace ContaBeeMovil.Services.Dev;

public static class LogSanitizer
{
    public static string EnmascararEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return "(vacío)";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..Math.Min(2, at)];
        var dominio = email[at..];
        return at < 2 ? $"***{dominio}" : $"{local}***{dominio}";
    }

    public static string EnmascararRfc(string? rfc)
    {
        if (string.IsNullOrEmpty(rfc)) return "(vacío)";
        if (rfc.Length < 5) return "***";
        return $"{rfc[..3]}***{rfc[^2..]}";
    }

    public static string EnmascararDispositivoId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "(vacío)";
        if (id.Length <= 4) return "***";
        return $"...{id[^4..]}";
    }

    public static string IndicarTokenObtenido() => "[token-obtenido]";
}
