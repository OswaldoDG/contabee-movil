using System.Net.Mail;

namespace ContaBeeMovil.Helpers;

/// <summary>
/// Separa y valida los correos de "Invitar". Mismo criterio que el back
/// (ServicioUsuarios.InvitarPorEmail): normaliza a minúsculas y exige punto en el dominio.
/// </summary>
public static class CorreosInvitacion
{
    /// <summary>Límite del back por petición.</summary>
    public const int Maximo = 5;

    private static readonly char[] Separadores = [',', ';', ' ', '\r', '\n', '\t'];

    /// <summary>Parte el texto pegado en correos normalizados. No quita duplicados.</summary>
    public static List<string> Separar(string? texto)
        => (texto ?? string.Empty)
           .Split(Separadores, StringSplitOptions.RemoveEmptyEntries)
           .Select(Normalizar)
           .ToList();

    public static string Normalizar(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool EsValido(string? email)
    {
        var normalizado = Normalizar(email);
        return MailAddress.TryCreate(normalizado, out var direccion)
               && direccion.Address == normalizado
               && direccion.Host.Contains('.');
    }
}
