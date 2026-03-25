using Contabee.Api.abstractions;
using Contabee.Api.Crm;



namespace Contabee.Api;

public class ServicioCrm(HttpClient httpClient) : IServicioCrm
{
    private readonly ServicioCRMClient servicioCrm = new (httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales()
    {
        RespuestaPayload<List<AsociacionCuentaFiscalCompleta>> r = new();

        try
        {
            var res = await servicioCrm.RfcAsync();
            r.Payload = res?.ToList() ?? new List<AsociacionCuentaFiscalCompleta>();
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            r.Payload = new List<AsociacionCuentaFiscalCompleta>();
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-GetAsociacionesFiscales");
        }

        return r;
    }

    public async Task<Respuesta> RegistrarCuentaFiscalMinima(Contabee.Api.Crm.CuentaFiscalMinima modelo)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.MinimaAsync(modelo);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-RegistrarCuentaFiscalMinima" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-RegistrarCuentaFiscalMinima");
        }

        return r;
    }

    public async Task<Respuesta> EliminarCuentaFiscal(string cuentaFiscalId)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.CuentafiscalDELETEAsync(Guid.Parse(cuentaFiscalId));
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EliminarCuentaFiscal" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EliminarCuentaFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EliminarAsociacionFiscal(long id)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.AsociacionfiscalDELETEAsync(id);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EliminarAsociacionFiscal" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EliminarAsociacionFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EnviarUrlCuentaFiscal(Contabee.Api.Crm.RequestUrl request)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.UrlAsync(request);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EnviarUrlCuentaFiscal" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EnviarUrlCuentaFiscal");
        }
        return r;
    }
}




