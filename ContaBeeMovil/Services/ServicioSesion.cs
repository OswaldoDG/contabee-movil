using CommunityToolkit.Maui.Core.Extensions;
using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Device;
using Microsoft.Maui.Storage;

namespace ContaBeeMovil.Services;

public class ServicioSesion : IServicioSesion
{
    public const string CLAVE_ID_DISPOSITIVO = "IdDispositivo";
    private const string CLAVE_ACCESS_TOKEN = "AccessToken";
    private const string CLAVE_REFRESH_TOKEN = "RefreshToken";
    private const string CLAVE_EMAIL = "CredencialEmail";
    private const string CLAVE_EXPIRACION = "TokenExpiracion";
    private readonly AppState _appState;
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioIdentidad _servicioIdentidad;

    public ServicioSesion(AppState appState,IServicioCrm servicioCrm,IServicioIdentidad servicioIdentidad)
    {
        _appState = appState;
        _servicioCrm = servicioCrm;
        _servicioIdentidad = servicioIdentidad;
    }

    public async Task<string> LeeIdDeDispositivo()
    {
        string? idDispositivo = await LeeContenidoClave(CLAVE_ID_DISPOSITIVO);
        if (string.IsNullOrEmpty(idDispositivo))
        {
            idDispositivo = Guid.NewGuid().ToString();
            await GuardaContenidoClave(CLAVE_ID_DISPOSITIVO, idDispositivo);
        }
        return idDispositivo;
    }

    public async Task GuardaTokenAsync(string accessToken, string refreshToken)
    {
        await GuardaContenidoClave(CLAVE_ACCESS_TOKEN, accessToken);
        await GuardaContenidoClave(CLAVE_REFRESH_TOKEN, refreshToken);
        Preferences.Set("TieneSesion", true);
    }

    public async Task GuardaExpiracionAsync(DateTime expiracion)
    {
        await GuardaContenidoClave(CLAVE_EXPIRACION, expiracion.ToString("O"));
    }

    public async Task<DateTime?> LeeExpiracionAsync()
    {
        var texto = await LeeContenidoClave(CLAVE_EXPIRACION);
        if (string.IsNullOrEmpty(texto)) return null;
        return DateTime.TryParse(texto, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    public Task<string?> LeeAccessTokenAsync() => LeeContenidoClave(CLAVE_ACCESS_TOKEN);

    public Task<string?> LeeRefreshTokenAsync() => LeeContenidoClave(CLAVE_REFRESH_TOKEN);

    public Task LimpiaTokensAsync()
    {
        SecureStorage.Remove(CLAVE_ACCESS_TOKEN);
        SecureStorage.Remove(CLAVE_REFRESH_TOKEN);
        Preferences.Set("TieneSesion", false);
        return Task.CompletedTask;
    }

    public async Task GuardaEmailAsync(string email)
    {
        await GuardaContenidoClave(CLAVE_EMAIL, email);
    }

    public async Task<string?> LeeEmailAsync()
    {
        return await LeeContenidoClave(CLAVE_EMAIL);
    }

    public Task LimpiaEmailAsync()
    {
        SecureStorage.Remove(CLAVE_EMAIL);
        return Task.CompletedTask;
    }

    public async Task GuardaContenidoClave(string clave, string contenido)
    {
        await SecureStorage.SetAsync(clave, contenido);
    }

    public async Task<string?> LeeContenidoClave(string clave)
    {
        var texto = await SecureStorage.GetAsync(clave);
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }
        return texto;
    }

    public async Task GetPerfilAsync()
    {
        var respuesta = await _servicioIdentidad.GetPerfil();
        if (respuesta.Ok) { 
        _appState.Perfil = respuesta.Payload; 
        }

    }

    public async Task GetAsociacionesFiscalesAsync()
    {
        var respuesta = await _servicioCrm.GetAsociacionesFiscales();
        if (respuesta.Ok)
        {
            _appState.CuentasFiscales = respuesta.Payload;
        }
    }


    public async Task PosLoginAsync()
    {
        await GetPerfilAsync();
        await GetAsociacionesFiscalesAsync();
    }
}
