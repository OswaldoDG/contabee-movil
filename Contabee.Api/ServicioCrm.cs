using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;

namespace Contabee.Api;

public class ServicioCrm(HttpClient httpClient) : IServicioCrm
{
    private readonly    ServicioCrmClient servicioCrm = new (httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales()
    {
        RespuestaPayload<List<AsociacionCuentaFiscalCompleta>> r = new();

        try
        {
            var res = await servicioCrm.RfcAsync();
            if(res!=null)
            {
                if(res.Count == 0)
                {
                    r.Payload = new List<AsociacionCuentaFiscalCompleta>();
                }
                else
                    r.Payload = res.ToList();
            }

            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Cuentas Fiscales");
        }

        return r;
    }
}
