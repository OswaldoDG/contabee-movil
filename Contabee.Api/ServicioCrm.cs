using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using System.Net.Http.Json;



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
            if (res != null && res.Count>0)
            {
                r.Payload = res.ToList();
                return r;

            }
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
        
            var httpResponse = await httpClient.PostAsJsonAsync("crm/rfc/minima", modelo);
            if (httpResponse.IsSuccessStatusCode)
            {
                r.Ok = true;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var content = await httpResponse.Content.ReadAsStringAsync();
                r.Error = new ErrorProceso { Mensaje = content, HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode, Origen = "ServicioCrm-RegistrarCuentaFiscalMinima" };
            }
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
            var httpResponse = await httpClient.DeleteAsync($"crm/cuentafiscal/{cuentaFiscalId}");
            if (httpResponse.IsSuccessStatusCode)
            {
                r.Ok = true;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var content = await httpResponse.Content.ReadAsStringAsync();
                r.Error = new ErrorProceso { Mensaje = content, HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode, Origen = "ServicioCrm-EliminarCuentaFiscal" };
            }
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
            var httpResponse = await httpClient.DeleteAsync($"asociacionfiscal/{id}");
            if (httpResponse.IsSuccessStatusCode)
            {
                r.Ok = true;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var content = await httpResponse.Content.ReadAsStringAsync();
                r.Error = new ErrorProceso { Mensaje = content, HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode, Origen = "ServicioCrm-EliminarAsociacionFiscal" };
            }
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
            var httpResponse = await httpClient.PostAsJsonAsync("crm/rfc/url", request);
            if (httpResponse.IsSuccessStatusCode)
            {
                r.Ok = true;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var content = await httpResponse.Content.ReadAsStringAsync();
                r.Error = new ErrorProceso { Mensaje = content, HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode, Origen = "ServicioCrm-EnviarUrlCuentaFiscal" };
            }
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EnviarUrlCuentaFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EnviarFeedback(DtoCreaRetroalimentacion request)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.FeedbackAsync(request);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EnviarFeedback");
        }
        return r;
    }
}
