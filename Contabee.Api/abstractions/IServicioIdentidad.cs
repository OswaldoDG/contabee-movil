using Contabee.Api.Identidad;

namespace Contabee.Api.abstractions;

public interface IServicioIdentidad
{
    Task<Respuesta> Registrar(RegisterViewModel request);
}
