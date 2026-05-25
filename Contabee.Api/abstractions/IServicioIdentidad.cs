using Contabee.Api.Identidad;

namespace Contabee.Api.abstractions;

public interface 
    IServicioIdentidad
{
    Task<Respuesta> Registrar(RegisterViewModel request);
    Task<RespuestaPayload<RespuestaToken>> IniciarSesion(string email, string password, string dispositivoId,bool recordarme);
    Task<Respuesta> ConfirmarCuenta(String token);
    Task<Respuesta> RecuperarPassword(string email);
    Task<RespuestaPayload<PerfilUsuario>> GetPerfil();
    Task<Respuesta> CambiarContrasena(string actual, string nueva);
    Task<Respuesta> RestablecerContrasena(string password, string token);
    Task<RespuestaPayload<List<CuentaUsuario>>> MisUsuarios(Guid cfid);
    Task<Respuesta> EliminarCuenta(string password);
    Task<RespuestaPayload<CuentaUsuario>> CrearUsuarioCaptura(CreaUsuarioCaptura usuarioCaptura, Guid cfid);
    Task<RespuestaPayload<RespuestaTokenVinculacion>> GetTokenVinculacion(string dispositivoId, bool enSesion = false);
    Task<RespuestaPayload<RespuestaTokenVinculacion>> ValidaTokenVinculacionEnSesion(string dispositivoId, string token);
    Task<RespuestaPayload<RespuestaTokenVinculacion>> ValidaTokenVinculacionSinSesion(string dispositivoId, string token);
    Task<RespuestaPayload<ResultadoTokenLoginLess>> GetTokenLoginLess(string dispositivoId);
    Task<RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload>> VincularUsuario(Guid cfid, SolictudVinculacion solictud);
    Task<RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload>> VincularUsuarioLoginLess(Guid cfid, SolictudTokenLoginless solictud);
    Task<Respuesta> EliminarVinculoUsuario(Guid cfid, Guid usuarioId);
}


