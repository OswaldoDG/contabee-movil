namespace ContaBeeMovil.Services;

public class ServicioSesion : IServicioSesion
{
    public const string CLAVE_ID_DISPOSITIVO = "IdDispositivo";
    private const string CLAVE_ACCESS_TOKEN = "AccessToken";
    private const string CLAVE_REFRESH_TOKEN = "RefreshToken";
    private const string CLAVE_EMAIL = "CredencialEmail";

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
}
