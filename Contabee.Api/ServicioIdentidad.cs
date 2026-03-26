using System.Text;
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

    public async Task<RespuestaPayload<RespuestaToken>> IniciarSesion(string email, string password, string dispositivoId,bool recordarme)
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
                ["scope"] = recordarme?"offline_access":null,
                ["dispositivoid"] = dispositivoId
            };


            var content = new FormUrlEncodedContent(formData);
            
            var httpResponse = await httpClient.PostAsync("/connect/token", content);

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
            servicioIdentidad.TokenAsync();
        }
        catch (Exception ex)
        {
            respuesta.Error = ex.ErrorGenerico("ServicioIdentidad-IniciarSesion");
        }

        return respuesta;
    }
    public async Task<Respuesta> ConfirmarCuenta(string token)
    {
        Respuesta r = new();

        try
        {
            await servicioIdentidad.ConfirmarPOSTAsync(token);
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Confirmar Cuenta");
        }

        return r;
    }

    public async Task<Respuesta> RecuperarPassword(string email)
    {
        Respuesta r = new();

        try
        {
            await servicioIdentidad.RecuperarAsync(email);
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Rescuperar Password");
        }

        return r;
    }

    public async Task<RespuestaPayload<PerfilUsuario>> GetPerfil()
    {
        RespuestaPayload<PerfilUsuario> r = new();

        try
        {
            var res = await servicioIdentidad.MiGETAsync();
            if(res != null)
            {
                r.Payload = new PerfilUsuario
                {
                    DisplayName = res.DisplayName,
                    EsInterno = res.EsInterno,
                    Iniciales = res.Iniciales,
                    Roles = res.Roles,
                    CuentaFiscalId = res.CuentaFiscalId
                };
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Perfil");
        }

        return r;

    }

    public async Task<Respuesta> RestablecerContrasena(string password, string token)
    {
        Respuesta r = new();

        try
        {
            var body = new RecuperacionContrasena
            {
                Password = password,
                Token = token
            };
            await servicioIdentidad.RestablecerAsync(body);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-RestablecerContrasena");
        }

        return r;
    }

    public async Task<Respuesta> CambiarContrasena(string actual, string nueva)
    {
        Respuesta r = new();

        try
        {
            var body = new { actual, nueva };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await httpClient.PostAsync("/api/identity/usuarios/mi/contrasena", content);
            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[CambiarContrasena] Status={httpResponse.StatusCode}, Body={responseJson}");

            if (httpResponse.IsSuccessStatusCode)
            {
                r.Ok = true;
                r.HttpCode = System.Net.HttpStatusCode.OK;
            }
            else
            {
                // La API puede devolver texto plano "CODIGO: Mensaje" o JSON
                var mensaje = "Error al cambiar la contraseña";
                var codigo = "cambiar_contrasena_error";

                var textoLimpio = responseJson.Trim().Trim('"');
                if (textoLimpio.Contains(':'))
                {
                    var partes = textoLimpio.Split(':', 2);
                    codigo = partes[0].Trim();
                    mensaje = partes[1].Trim();
                }
                else if (!string.IsNullOrEmpty(textoLimpio))
                {
                    mensaje = textoLimpio;
                }

                r.Error = new ErrorProceso
                {
                    Codigo = codigo,
                    Mensaje = mensaje,
                    Origen = "ServicioIdentidad-CambiarContrasena",
                    HttpCode = (System.Net.HttpStatusCode)httpResponse.StatusCode
                };
            }
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-CambiarContrasena");
        }

        return r;
    }

    public async Task<Respuesta> EliminarCuenta(string password)
    {
        Respuesta r = new();

        try
        {
            await servicioIdentidad.MiDELETEAsync(new DTOEliminarUsuario { Contrasena = password});
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-EliminarCuenta");
        }

        return r;
    }

    public async Task<RespuestaPayload<List<CuentaUsuario>>> MisUsuarios(Guid cfid)
    {
        RespuestaPayload<List<CuentaUsuario>> r = new();

        try
        {   var res= await servicioIdentidad.ObtieneUsuariosCuentaFiscalAsync(cfid);
            r.Payload = res.ToList();
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Obtener Usuarios");
        }

        return r;
    }
}
