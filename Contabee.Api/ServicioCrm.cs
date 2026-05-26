using Contabee.Api.abstractions;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using Contabee.Api.Logging;
using System.Net.Http.Json;



namespace Contabee.Api;

public class ServicioCrm(HttpClient httpClient, IAppLogger logger) : IServicioCrm
{
    private readonly ServicioCRMClient servicioCrm = new (httpClient.BaseAddress!.ToString(), httpClient);
    private readonly IAppLogger _logger = logger;

    public async Task<RespuestaPayload<List<AsociacionCuentaFiscalCompleta>>> GetAsociacionesFiscales()
    {
        RespuestaPayload<List<AsociacionCuentaFiscalCompleta>> r = new();

        try
        {
            _logger.Info("ServicioCrm.GetAsociacionesFiscales", "Inicio de consulta de asociaciones fiscales.");
            var res = await servicioCrm.RfcAsync();
            r.Payload = res?.ToList() ?? new List<AsociacionCuentaFiscalCompleta>();
            r.Ok = true;
            _logger.Info("ServicioCrm.GetAsociacionesFiscalesExitoso", "Consulta de asociaciones fiscales completada.");
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            _logger.Debug("ServicioCrm.GetAsociacionesFiscalesNotFound", "API devolvió 404, se regresará lista vacía.", ex);
            r.Payload = new List<AsociacionCuentaFiscalCompleta>();
            r.Ok = true;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.GetAsociacionesFiscalesException", "Excepción no controlada al consultar asociaciones fiscales.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-GetAsociacionesFiscales");
        }

        return r;
    }

    public async Task<Respuesta> RegistrarCuentaFiscalMinima(Contabee.Api.Crm.CuentaFiscalMinima modelo)
    {
        var r = new Respuesta();
        try
        {
            _logger.Info("ServicioCrm.RegistrarCuentaFiscalMinima", "Inicio de registro de cuenta fiscal mínima.");
            await servicioCrm.MinimaAsync(modelo);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioCrm.RegistrarCuentaFiscalMinimaExitoso", "Registro de cuenta fiscal mínima completado.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioCrm.RegistrarCuentaFiscalMinimaApiException", "Error API al registrar cuenta fiscal mínima.", ex);
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-RegistrarCuentaFiscalMinima" };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.RegistrarCuentaFiscalMinimaException", "Excepción no controlada al registrar cuenta fiscal mínima.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-RegistrarCuentaFiscalMinima");
        }

        return r;
    }

    public async Task<Respuesta> EliminarCuentaFiscal(string cuentaFiscalId)
    {
        var r = new Respuesta();
        try
        {
            _logger.Info("ServicioCrm.EliminarCuentaFiscal", "Inicio de eliminación de cuenta fiscal.");
            await servicioCrm.CuentafiscalDELETEAsync(Guid.Parse(cuentaFiscalId));
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioCrm.EliminarCuentaFiscalExitoso", "Eliminación de cuenta fiscal completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioCrm.EliminarCuentaFiscalApiException", "Error API al eliminar cuenta fiscal.", ex);
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EliminarCuentaFiscal" };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.EliminarCuentaFiscalException", "Excepción no controlada al eliminar cuenta fiscal.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-EliminarCuentaFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EliminarAsociacionFiscal(long id)
    {
        var r = new Respuesta();
        try
        {
            _logger.Info("ServicioCrm.EliminarAsociacionFiscal", "Inicio de eliminación de asociación fiscal.");
            await servicioCrm.AsociacionfiscalDELETEAsync(id);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioCrm.EliminarAsociacionFiscalExitoso", "Eliminación de asociación fiscal completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioCrm.EliminarAsociacionFiscalApiException", "Error API al eliminar asociación fiscal.", ex);
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EliminarAsociacionFiscal" };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.EliminarAsociacionFiscalException", "Excepción no controlada al eliminar asociación fiscal.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-EliminarAsociacionFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EnviarUrlCuentaFiscal(Contabee.Api.Crm.RequestUrl request)
    {
        var r = new Respuesta();
        try
        {
            _logger.Info("ServicioCrm.EnviarUrlCuentaFiscal", "Inicio de envío de URL de cuenta fiscal.");
            await servicioCrm.UrlAsync(request);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioCrm.EnviarUrlCuentaFiscalExitoso", "Envío de URL de cuenta fiscal completado.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioCrm.EnviarUrlCuentaFiscalApiException", "Error API al enviar URL de cuenta fiscal.", ex);
            r.Error = new ErrorProceso { Mensaje = ex.Response, HttpCode = (System.Net.HttpStatusCode)ex.StatusCode, Origen = "ServicioCrm-EnviarUrlCuentaFiscal" };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.EnviarUrlCuentaFiscalException", "Excepción no controlada al enviar URL de cuenta fiscal.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-EnviarUrlCuentaFiscal");
        }
        return r;
    }

    public async Task<Respuesta> EnviarFeedback(DtoCreaRetroalimentacion request)
    {
        var r = new Respuesta();
        try
        {
            _logger.Info("ServicioCrm.EnviarFeedback", "Inicio de envío de retroalimentación.");
            await servicioCrm.FeedbackAsync(request);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioCrm.EnviarFeedbackExitoso", "Envío de retroalimentación completado.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.EnviarFeedbackException", "Excepción no controlada al enviar retroalimentación.", ex);
            r.Error = ex.ErrorGenerico("ServicioCrm-EnviarFeedback");
        }
        return r;
    }
    public async Task<RespuestaPayload<DtoLicenciamiento2>> GetLicenciamiento(Guid cfid)
    {
        RespuestaPayload<DtoLicenciamiento2> r = new();

        try
        {
            _logger.Info("ServicioCrm.GetLicenciamiento", "Inicio de consulta de licenciamiento.");

            var res = await servicioCrm.LicenciamientoAsync(cfid,null);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
            _logger.Info("ServicioCrm.GetLicenciamientoExitoso", "Consulta de licenciamiento completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioCrm.GetLicenciamientoException", "Excepción no controlada al consultar licenciamiento.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Licenciamiento");
        }

        return r;
    }
}
