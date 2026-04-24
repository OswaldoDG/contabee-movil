using Contabee.Api.Crm;

namespace Contabee.Api.abstractions;

public interface IServicioCrm
{
    Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales();
    Task<Respuesta> RegistrarCuentaFiscalMinima(CuentaFiscalMinima modelo);
    Task<Respuesta> EnviarUrlCuentaFiscal(RequestUrl request);
    Task<Respuesta> EliminarCuentaFiscal(string cuentaFiscalId);
    Task<Respuesta> EliminarAsociacionFiscal(long id);
    Task<Respuesta> EnviarFeedback(DtoCreaRetroalimentacion request);
    Task<RespuestaPayload<DtoLicenciamiento2>> GetLicenciamiento(Guid cfid);
    Task<RespuestaPayload<RespuestaSolicitudLicenciamientoDemo>> SolicitarLicenciamientoDemo(string rfc, string dispositivoId, Guid? cfid, string cupon);
    Task<RespuestaPayload<LicenciamientoCuentaFiscal>> ActivarLicenciamientoDemo(string token, string dispositivoId, Guid? cfid);
    Task<RespuestaPayload<List<LicenciamientoGratuito>>> GetLicenciamientosFactibles(string rfc, string dispositivoId, Guid? cfid);
}
