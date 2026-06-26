using Contabee.Api.Crm;

namespace Contabee.Api.abstractions;

public interface IServicioCrm
{
    Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales();
    Task<Respuesta> RegistrarCuentaFiscalMinima(CuentaFiscalMinima modelo);
    Task<Respuesta> ActualizaRFCMinima(CuentaFiscalMinima modelo);
    Task<Respuesta> EnviarUrlCuentaFiscal(RequestUrl request);
    Task<RespuestaPayload<CuentaFiscal>> PreviewUrlCuentaFiscal(RequestUrlInfo request);
    Task<Respuesta> EliminarCuentaFiscal(string cuentaFiscalId);
    Task<Respuesta> EliminarAsociacionFiscal(long id);
    Task<Respuesta> EnviarFeedback(DtoCreaRetroalimentacion request);
    Task<RespuestaPayload<DtoLicenciamiento2>> GetLicenciamiento(Guid cfid);
    Task<RespuestaPayload<List<TarjetaUsuario>>> MisTarjetasUsuario();
    Task<Respuesta> GuardarMisTarjetasUsuario(List<TarjetaUsuario> tarjetas);
    Task<RespuestaPayload<List<PropiedadUsuarioCF>>> GetPropiedadesUsuario(Guid cfid, Guid usuarioId);
    Task<Respuesta> SetPropiedadUsuario(Guid cfid, Guid usuarioId, string prop, string valor);
    Task<Respuesta> EliminarPropiedadUsuario(Guid cfid, Guid usuarioId, string prop);
    Task<Respuesta> SetActivaAsociacion(Guid cfid, Guid usuarioId, bool activa);
}
