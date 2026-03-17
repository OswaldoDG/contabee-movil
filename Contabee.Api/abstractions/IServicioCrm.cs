using Contabee.Api.Crm;

namespace Contabee.Api.abstractions;

public interface IServicioCrm
{
    Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales();
    Task<Respuesta> RegistrarCuentaFiscalMinima(CuentaFiscalMinima modelo);
    Task<Respuesta> EnviarUrlCuentaFiscal(RequestUrl request);

}
