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
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            r.Error = new ErrorProceso
            {
                HttpCode = System.Net.HttpStatusCode.Conflict,
                Mensaje = "Email duplicado",
                Origen = "ServicioIdentidad-Registrar"
            };
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Mensaje = string.IsNullOrWhiteSpace(ex.Response) ? ex.Message : ex.Response,
                Origen = "ServicioIdentidad-Registrar"
            };
        }
		catch (Exception ex)
		{
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Registrar");
		}

        return r;
    }

    public async Task<RespuestaBoolean> ExisteSolicitudConfirmacion(string id)
    {
        RespuestaBoolean r = new();

        try
        {
            await servicioIdentidad.ConfirmarGETAsync(id);
            r.Resultado = true;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            r.Resultado = false;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-ExisteSolicitudConfirmacion");
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
                ["dispositivoid"] = dispositivoId
            };

            if (recordarme)
                formData["scope"] = "offline_access";

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
            await servicioIdentidad.ConfirmarPOSTAsync(token, false);
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

    public async Task<RespuestaPayload<CuentaUsuarioResultadoPaginado>> BuscarUsuarios(Guid cfid, Busqueda busqueda)
    {
        RespuestaPayload<CuentaUsuarioResultadoPaginado> r = new();

        try
        {
            var res = await servicioIdentidad.Buscar4Async(cfid, busqueda);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Buscar Usuarios");
        }

        return r;
    }

    public async Task<RespuestaPayload<CuentaUsuario>> CrearUsuarioCaptura(CreaUsuarioCaptura usuarioCaptura, Guid cfid)
    {
        RespuestaPayload<CuentaUsuario> r = new();

        try
        {
            var res = await servicioIdentidad.CapturaAsync(cfid,usuarioCaptura);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Crear Usuario Captura");
        }

        return r;
    }

    public async Task<RespuestaPayload<RespuestaTokenVinculacion>> GetTokenVinculacion(string dispositivoId, bool enSesion)
    {
        RespuestaPayload<RespuestaTokenVinculacion> r = new();

        try
        {
            var res = enSesion
                ? await servicioIdentidad.SesionAsync(dispositivoId)
                : await servicioIdentidad.TokenvinculacionAsync(dispositivoId);

            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            r.HttpCode = System.Net.HttpStatusCode.Conflict;
            r.Error = ex.ErrorGenerico("ServicioIdentidad-GetTokenVinculacion");
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Obtener  token vinculacion");
        }

        return r;
    }

    public async Task<RespuestaPayload<RespuestaTokenVinculacion>> ValidaTokenVinculacionSinSesion(string dispositivoId, string token)
    {
        RespuestaPayload<RespuestaTokenVinculacion> r = new();

        try
        {
            var res = await servicioIdentidad.Tokenvinculacion2Async(dispositivoId, token);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 200)
        {
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Validar token vinculacion sin Sesion");
        }

        return r;
    }

    public async Task<RespuestaPayload<RespuestaTokenVinculacion>> ValidaTokenVinculacionEnSesion(string dispositivoId, string token)
    {
        RespuestaPayload<RespuestaTokenVinculacion> r = new();

        try
        {
            var res = await servicioIdentidad.VinculadoAsync(dispositivoId, token);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 200)
        {
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Validar token vinculacion en Sesion");
        }

        return r;
    }

    public async Task<RespuestaPayload<ResultadoTokenLoginLess>> GetTokenLoginLess(string dispositivoId)
    {
        RespuestaPayload<ResultadoTokenLoginLess> r = new();

        try
        {
            var res = await servicioIdentidad.TokenloginlessGETAsync(dispositivoId);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicita token LoginLess");
        }

        return r;
    }

    public async Task<RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload>> VincularUsuario(Guid cfid,SolictudVinculacion solictud)
    {
        RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload> r = new();

        try
        {
            var res = await servicioIdentidad.VincularAsync(cfid,solictud);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex) when (ex.Message.Contains("Status: 200"))
        {
            // Servidor devuelve 200 con body vacío — NSwag lanza excepción pero es éxito
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            r.HttpCode = System.Net.HttpStatusCode.Conflict;
            r.Error = ex.ErrorGenerico("ServicioIdentidad-VincularUsuario");
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicitar Vincular Usuario en Sesion");
        }

        return r;
    }

    public async Task<RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload>> VincularUsuarioLoginLess(Guid cfid, SolictudTokenLoginless solictud)
    {
        RespuestaPayload<ResultadoTokenLoginLessRespuestaPayload> r = new();

        try
        {
            var res = await servicioIdentidad.TokenloginlessPOSTAsync(cfid,solictud);
            r.Payload = res;
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex) when (ex.Message.Contains("Status: 200"))
        {
            // Servidor devuelve 200 con body vacío — NSwag lanza excepción pero es éxito
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            r.HttpCode = System.Net.HttpStatusCode.Conflict;
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicita Vinculacion Usuario LoginLess");
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicita Vinculacion Usuario LoginLess");
        }

        return r;
    }

    public async Task<Respuesta> EliminarAsociacionesDispositivo(string dispositivoId)
    {
        Respuesta r = new();
        try
        {
            await servicioIdentidad.DispositivoAsync(dispositivoId);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-EliminarAsociacionesDispositivo");
        }
        return r;
    }

    public async Task<Respuesta> EliminarVinculoUsuario(Guid cfid,Guid usuarioId)
    {
        Respuesta r = new();

        try
        {
            await servicioIdentidad.CuentafiscalAsync(usuarioId,cfid); 
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Solicita Vinculacion Usuario LoginLess");
        }

        return r;
    }


} 
