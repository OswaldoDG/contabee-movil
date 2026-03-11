

namespace ContaBeeMovil.Services;

public interface IServicioSesion
{
    Task<string> LeeIdDeDispositivo();
    Task GuardaTokenAsync(string accessToken, string refreshToken);
    Task<string?> LeeAccessTokenAsync();
    Task<string?> LeeRefreshTokenAsync();
    Task LimpiaTokensAsync();
    Task GuardaEmailAsync(string email);
    Task<string?> LeeEmailAsync();
    Task LimpiaEmailAsync();
    Task GetPerfilAsync();

    Task GetAsociacionesFiscalesAsync();

    Task PosLoginAsync();
}
