using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
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
            r.Payload = res?.ToList() ?? new List<AsociacionCuentaFiscalCompleta>();
            r.Ok = true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            r.Payload = new List<AsociacionCuentaFiscalCompleta>();
            r.Ok = true;
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
    public async Task<RespuestaPayload<DtoLicenciamiento2>> GetLicenciamiento(Guid cfid)
    {
        RespuestaPayload<DtoLicenciamiento2> r = new();

        try
        {

            var res = await servicioCrm.LicenciamientoAsync(cfid,null);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Licenciamiento");
        }

        return r;

    }

    public async Task<RespuestaPayload<RespuestaSolicitudLicenciamientoDemo>> SolicitarLicenciamientoDemo(string rfc, string dispositivoId,Guid? cfid)
    {
        RespuestaPayload<RespuestaSolicitudLicenciamientoDemo> r = new();

        try
        {

            var res = await servicioCrm.SolicitarAsync(rfc,dispositivoId,cfid);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicitar Licencia Demo");
        }

        return r;

    }

    public async Task<RespuestaPayload<LicenciamientoCuentaFiscal>> ActivarLicenciamientoDemo(string token, string dispositivoId, Guid? cfid)
    {
        RespuestaPayload<LicenciamientoCuentaFiscal> r = new();

        try
        {

            var res = await servicioCrm.ActivarAsync(token,dispositivoId,cfid);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Activar Licencia Demo");
        }

        return r;

    }
}
