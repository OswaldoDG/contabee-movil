using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using System.Net.Http.Json;

namespace Contabee.Api;

public class ServicioCrm : IServicioCrm
{
    private readonly HttpClient _httpClient;

    public ServicioCrm(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales()
    {
        var r = new RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>();
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<AsociacionCuentaFiscalCompleta>>("rfc");
            r.Payload = res ?? new List<AsociacionCuentaFiscalCompleta>();
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
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
            var httpResponse = await _httpClient.PostAsJsonAsync("rfc/minima", modelo);
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

    public async Task<Respuesta> EnviarUrlCuentaFiscal(Contabee.Api.Crm.RequestUrl request)
    {
        var r = new Respuesta();
        try
        {
            var httpResponse = await _httpClient.PostAsJsonAsync("rfc/url", request);
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
}
