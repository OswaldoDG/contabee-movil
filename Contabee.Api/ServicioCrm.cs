using Contabee.Api.abstractions;
using Contabee.Api.crm;
using Contabee.Api.Crm;
using Contabee.Api.Identidad;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection.Metadata;
using System.Text.Json;


namespace Contabee.Api;

public class ServicioCrm(HttpClient httpClient) : IServicioCrm
{
    private readonly    ServicioCrmClient servicioCrm = new (httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<RespuestaPayload<List<CuentaUsuarioResponse>>> GetAsociacionesFiscales()
    {
        RespuestaPayload<List<CuentaUsuarioResponse>> r = new();

        try
        {
            //var res = await servicioCrm.RfcAsync();
            //if(res!=null)
            //{
            //    if(res.Count == 0)
            //    {
            //        r.Payload = new List<AsociacionCuentaFiscalCompleta>();
            //    }
            //    else
            //        r.Payload = res.ToList();
            //}


            var httpResponse = await httpClient.GetAsync("api/crm/crm/rfc");

            var json = await httpResponse.Content.ReadAsStringAsync();

            if (httpResponse.IsSuccessStatusCode)
            {
                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new DefaultContractResolver(),
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };

                var cuentas = JsonConvert.DeserializeObject<List<CuentaUsuarioResponse>>(json, settings);
                r.Payload = cuentas;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var errorToken = JsonConvert.DeserializeObject<ErrorToken>(json);
                r.Error = new ErrorProceso
                {
                    Codigo = errorToken?.Error ?? "login_error",
                    Mensaje = errorToken?.ErrorDescription ?? "Error al iniciar sesión",
                    Origen = "ServicioIdentidad-IniciarSesion",
                    HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode
                };
            }
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Cuentas Fiscales");
        }

        return r;
    }
}



