using System.Net;
using System.Text.RegularExpressions;

namespace Contabee.Api;

public static class Extensiones
{
    /// <summary>
    /// Error genérico.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static ErrorProceso ErrorGenerico(this Exception exception, string origen)
    {
        //return new ErrorProceso() { HttpCode = HttpStatusCode.InternalServerError, Mensaje = exception.Message, Origen = origen};
        string rawError = exception.Message;

        // 1. Intentamos extraer el código y el mensaje real del string sucio
        var statusMatch = Regex.Match(rawError, @"Status:\s*(\d+)");
        var messageMatch = Regex.Match(rawError, @":\s*([^""]+)""?$");

        // 2. Si encontramos un código numérico, lo convertimos a HttpStatusCode
        // Si no, por defecto usamos InternalServerError (500)
        HttpStatusCode code = statusMatch.Success
            ? (HttpStatusCode)int.Parse(statusMatch.Groups[1].Value)
            : HttpStatusCode.InternalServerError;

        // 3. Si encontramos el mensaje limpio (ej: "Email no encontrado"), lo usamos
        // Si no, usamos el mensaje original de la excepción
        string mensajeLimpio = messageMatch.Success
            ? messageMatch.Groups[1].Value.Trim()
            : rawError;

        return new ErrorProceso()
        {
            HttpCode = code,
            Mensaje = mensajeLimpio,
            Origen = origen
        };
    }
}
