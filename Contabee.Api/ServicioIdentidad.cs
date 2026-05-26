using System.Text;
using System.Text.Json;
using Contabee.Api.abstractions;
using Contabee.Api.Identidad;
using Contabee.Api.Logging;

namespace Contabee.Api;

public class ServicioIdentidad(HttpClient httpClient, IAppLogger logger) : IServicioIdentidad
{
    private readonly ServicioIdentidadClient servicioIdentidad = new (httpClient.BaseAddress!.ToString(), httpClient);
    private readonly IAppLogger _logger = logger;



    public async Task<Respuesta> Registrar(RegisterViewModel request )
    {
        Respuesta r = new ();

        try
		{
         _logger.Info("ServicioIdentidad.Registrar", "Inicio de registro de usuario.");
			await servicioIdentidad.RegistroAsync(true, request);
            r.Ok = true;
        _logger.Info("ServicioIdentidad.RegistrarExitoso", "Registro de usuario completado.");
        }
		catch (Exception ex)
		{
         _logger.Debug("ServicioIdentidad.RegistrarException", "Excepción no controlada al registrar usuario.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Registrar");
		}

        return r;
    }

    public async Task<RespuestaPayload<RespuestaToken>> IniciarSesion(string email, string password, string dispositivoId,bool recordarme)
    {
        var respuesta = new RespuestaPayload<RespuestaToken>();

        try
        {
            _logger.Info("ServicioIdentidad.IniciarSesion", "Inicio de autenticación.");
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
                _logger.Info("ServicioIdentidad.IniciarSesionExitoso", "Autenticación completada correctamente.");
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
                _logger.Debug("ServicioIdentidad.IniciarSesionError", "Error de autenticación.", new Dictionary<string, object?>
                {
                    ["HttpCode"] = (int)httpResponse.StatusCode,
                    ["Codigo"] = respuesta.Error.Codigo
                });
            }
            servicioIdentidad.TokenAsync();
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.IniciarSesionException", "Excepción no controlada al autenticar usuario.", ex);
            respuesta.Error = ex.ErrorGenerico("ServicioIdentidad-IniciarSesion");
        }

        return respuesta;
    }
    public async Task<Respuesta> ConfirmarCuenta(string token)
    {
        Respuesta r = new();

        try
        {
            _logger.Info("ServicioIdentidad.ConfirmarCuenta", "Inicio de confirmación de cuenta.");
            await servicioIdentidad.ConfirmarPOSTAsync(token);
            r.Ok = true;
            _logger.Info("ServicioIdentidad.ConfirmarCuentaExitoso", "Confirmación de cuenta completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.ConfirmarCuentaException", "Excepción no controlada al confirmar cuenta.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Confirmar Cuenta");
        }

        return r;
    }

    public async Task<Respuesta> RecuperarPassword(string email)
    {
        Respuesta r = new();

        try
        {
            _logger.Info("ServicioIdentidad.RecuperarPassword", "Inicio de recuperación de contraseña.");
            await servicioIdentidad.RecuperarAsync(email);
            r.Ok = true;
            _logger.Info("ServicioIdentidad.RecuperarPasswordExitoso", "Solicitud de recuperación completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.RecuperarPasswordException", "Excepción no controlada al recuperar contraseña.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Rescuperar Password");
        }

        return r;
    }

    public async Task<RespuestaPayload<PerfilUsuario>> GetPerfil()
    {
        RespuestaPayload<PerfilUsuario> r = new();

        try
        {
            _logger.Info("ServicioIdentidad.GetPerfil", "Inicio de consulta de perfil.");
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
            _logger.Info("ServicioIdentidad.GetPerfilExitoso", "Consulta de perfil completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.GetPerfilException", "Excepción no controlada al obtener perfil.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get Perfil");
        }

        return r;

    }

    public async Task<Respuesta> RestablecerContrasena(string password, string token)
    {
        Respuesta r = new();

        try
        {
            _logger.Info("ServicioIdentidad.RestablecerContrasena", "Inicio de restablecimiento de contraseña.");
            var body = new RecuperacionContrasena
            {
                Password = password,
                Token = token
            };
            await servicioIdentidad.RestablecerAsync(body);
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioIdentidad.RestablecerContrasenaExitoso", "Restablecimiento de contraseña completado.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.RestablecerContrasenaException", "Excepción no controlada al restablecer contraseña.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-RestablecerContrasena");
        }

        return r;
    }

    public async Task<Respuesta> CambiarContrasena(string actual, string nueva)
    {
        Respuesta r = new();

        try
        {
            _logger.Info("ServicioIdentidad.CambiarContrasena", "Inicio de cambio de contraseña.");
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
                _logger.Info("ServicioIdentidad.CambiarContrasenaExitoso", "Cambio de contraseña completado.");
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
                _logger.Debug("ServicioIdentidad.CambiarContrasenaError", "Error al cambiar contraseña.", new Dictionary<string, object?>
                {
                    ["HttpCode"] = (int)httpResponse.StatusCode,
                    ["Codigo"] = codigo
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.CambiarContrasenaException", "Excepción no controlada al cambiar contraseña.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-CambiarContrasena");
        }

        return r;
    }

    public async Task<Respuesta> EliminarCuenta(string password)
    {
        Respuesta r = new();

        try
        {
            _logger.Info("ServicioIdentidad.EliminarCuenta", "Inicio de eliminación de cuenta.");
            await servicioIdentidad.MiDELETEAsync(new DTOEliminarUsuario { Contrasena = password});
            r.Ok = true;
            _logger.Info("ServicioIdentidad.EliminarCuentaExitoso", "Eliminación de cuenta completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.EliminarCuentaException", "Excepción no controlada al eliminar cuenta.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-EliminarCuenta");
        }

        return r;
    }

    public async Task<RespuestaPayload<List<CuentaUsuario>>> MisUsuarios(Guid cfid)
    {
        RespuestaPayload<List<CuentaUsuario>> r = new();

        try
        {
            _logger.Info("ServicioIdentidad.MisUsuarios", "Inicio de consulta de usuarios por cuenta fiscal.");
            var res= await servicioIdentidad.ObtieneUsuariosCuentaFiscalAsync(cfid);
            r.Payload = res.ToList();
            r.Ok = true;
            r.HttpCode = System.Net.HttpStatusCode.OK;
            _logger.Info("ServicioIdentidad.MisUsuariosExitoso", "Consulta de usuarios completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioIdentidad.MisUsuariosException", "Excepción no controlada al obtener usuarios de cuenta fiscal.", ex);
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Obtener Usuarios");
        }

        return r;
    }
}
