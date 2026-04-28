using ContaBeeMovil.Models;

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
    Task GuardaExpiracionAsync(DateTime expiracion);
    Task<DateTime?> LeeExpiracionAsync();
    Task GetPerfilAsync();
    Task GetAsociacionesFiscalesAsync();
    Task GetLicenciaAsync();
    Task GetMisUsuariosAsync();
    Task GetTarjetasAsync();
    Task GuardarTarjetasAsync(List<TarjetaModel> tarjetas);
    Task PosLoginAsync();
    Task VerificarSesionAlReanudarAsync();
    Task CerrarSesionAsync();
    Task PostEliminarCuentaAsync();
}
