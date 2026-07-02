using ContaBeeMovil.Models;

namespace ContaBeeMovil.Services;

public interface IServicioSesion
{
    Task<string> LeeIdDeDispositivo();
    Task GuardaTokenAsync(string accessToken, string refreshToken);
    Task<string?> LeeAccessTokenAsync();
    Task<string?> LeeRefreshTokenAsync();
    Task LimpiaTokensAsync(bool conservarLoginLess = false);
    Task GuardaEmailAsync(string email);
    Task<string?> LeeEmailAsync();
    Task LimpiaEmailAsync();
    Task GuardaExpiracionAsync(DateTime expiracion);
    Task<DateTime?> LeeExpiracionAsync();
    Task GetPerfilAsync();
    Task GetAsociacionesFiscalesAsync();
    void AplicarCuentasFiscales(List<Contabee.Api.Crm.AsociacionCuentaFiscalCompleta> cuentas);
    Task GetLicenciaAsync();
    Task GetMisUsuariosAsync();
    Task GetTarjetasAsync();
    Task GuardarTarjetasAsync(List<TarjetaModel> tarjetas);
    Task PosLoginAsync();
    Task VerificarSesionAlReanudarAsync();
    Task CerrarSesionAsync();
    Task ManejarDesvinculacionAsync(TipoAccesoPerdido tipo = TipoAccesoPerdido.Desconocido);
    Task<bool> RefrescarAccesoAsync();
    Task PostEliminarCuentaAsync();
    Task GuardaTokenLoginLessAsync(string token);
    Task<string?> LeeTokenLoginLessAsync();
    Task LimpiaTokenLoginLessAsync();
    Task<bool> IntentarReanudarLoginLessAsync();
}
