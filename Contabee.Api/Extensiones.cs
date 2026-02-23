using System.Net;

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
        return new ErrorProceso() { HttpCode = HttpStatusCode.InternalServerError, Mensaje = exception.Message, Origen = origen};
    }
}
