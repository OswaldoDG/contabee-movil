using System.Text.Json;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;

namespace Contabee.Api;

public class ServicioIdentidad(HttpClient httpClient) : IServicioIdentidad
{
    private readonly ServicioIdentidadClient servicioIdentidad = new (httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<Respuesta> Registrar(RegisterViewModel request )
    {
        Respuesta r = new ();

        try
		{
			await servicioIdentidad.RegistroAsync(true, request);
            r.Ok = true;
        }
		catch (Exception ex)
		{
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Registrar");
		}

        return r;
    }

    public async Task<RespuestaPayload<RespuestaToken>> IniciarSesion(string email, string password, string dispositivoId)
    {
        var respuesta = new RespuestaPayload<RespuestaToken>();

        try
        {
            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "contabee-password",
                ["username"] = email,
                ["password"] = password,
                ["scope"] = "offline_access",
                ["dispositivoid"] = dispositivoId
            };

            var content = new FormUrlEncodedContent(formData);
            var httpResponse = await httpClient.PostAsync("api/identity/connect/token", content);

            var json = await httpResponse.Content.ReadAsStringAsync();

            if (httpResponse.IsSuccessStatusCode)
            {
                respuesta.Payload = JsonSerializer.Deserialize<RespuestaToken>(json);
                respuesta.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                var errorToken = JsonSerializer.Deserialize<ErrorToken>(json);
                respuesta.Error = new ErrorProceso
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
            respuesta.Error = ex.ErrorGenerico("ServicioIdentidad-IniciarSesion");
        }

        return respuesta;
    }
}
