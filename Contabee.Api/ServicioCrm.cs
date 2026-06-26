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
            await servicioCrm.MinimaPOSTAsync(modelo);
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

    public async Task<Respuesta> ActualizaRFCMinima(Contabee.Api.Crm.CuentaFiscalMinima modelo)
    {
        var r = new Respuesta();
        try
        {
            await servicioCrm.MinimaPUTAsync(modelo);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-ActualizaRFCMinima" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-ActualizaRFCMinima");
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

    public async Task<RespuestaPayload<CuentaFiscal>> PreviewUrlCuentaFiscal(Contabee.Api.Crm.RequestUrlInfo request)
    {
        RespuestaPayload<CuentaFiscal> r = new();
        try
        {
            var res = await servicioCrm.PreviewAsync(request);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-PreviewUrlCuentaFiscal" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-PreviewUrlCuentaFiscal");
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

    public async Task<RespuestaPayload<List<TarjetaUsuario>>> MisTarjetasUsuario()
    {
        RespuestaPayload<List<TarjetaUsuario>> r = new();
        try
        {
            var res = await servicioCrm.TarjetasAllAsync();
            r.Payload = res?.ToList() ?? [];
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-MisTarjetasUsuario");
        }
        return r;
    }

    public async Task<Respuesta> GuardarMisTarjetasUsuario(List<TarjetaUsuario> tarjetas)
    {
        Respuesta r = new();
        try
        {
            await servicioCrm.TarjetasAsync(tarjetas);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-GuardarMisTarjetasUsuario");
        }
        return r;
    }

    public async Task<RespuestaPayload<List<PropiedadUsuarioCF>>> GetPropiedadesUsuario(Guid cfid, Guid usuarioId)
    {
        RespuestaPayload<List<PropiedadUsuarioCF>> r = new();
        try
        {
            var res = await servicioCrm.PropiedadAllAsync(cfid, usuarioId);
            r.Payload = res?.ToList() ?? [];
            r.Ok = true;
        }
        catch (ApiException ex) when (ex.StatusCode == 403)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = System.Net.HttpStatusCode.Forbidden, Origen = "ServicioCrm-GetPropiedadesUsuario" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-GetPropiedadesUsuario");
        }
        return r;
    }

    public async Task<Respuesta> SetPropiedadUsuario(Guid cfid, Guid usuarioId, string prop, string valor)
    {
        Respuesta r = new();
        try
        {
            await servicioCrm.PropiedadPOSTAsync(cfid, usuarioId, prop, valor);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 403)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = System.Net.HttpStatusCode.Forbidden, Origen = "ServicioCrm-SetPropiedadUsuario" };
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-SetPropiedadUsuario" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-SetPropiedadUsuario");
        }
        return r;
    }

    public async Task<Respuesta> SetActivaAsociacion(Guid cfid, Guid usuarioId, bool activa)
    {
        Respuesta r = new();
        try
        {
            await servicioCrm.ActivaAsync(cfid, usuarioId, activa);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 403)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = System.Net.HttpStatusCode.Forbidden, Origen = "ServicioCrm-SetActivaAsociacion" };
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-SetActivaAsociacion" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-SetActivaAsociacion");
        }
        return r;
    }

    public async Task<Respuesta> EliminarPropiedadUsuario(Guid cfid, Guid usuarioId, string prop)
    {
        Respuesta r = new();
        try
        {
            await servicioCrm.PropiedadDELETEAsync(cfid, usuarioId, prop);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            r.Ok = true;
        }
        catch (ApiException ex) when (ex.StatusCode == 403)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = System.Net.HttpStatusCode.Forbidden, Origen = "ServicioCrm-EliminarPropiedadUsuario" };
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EliminarPropiedadUsuario" };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioCrm-EliminarPropiedadUsuario");
        }
        return r;
    }
}
