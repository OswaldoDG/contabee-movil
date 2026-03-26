using System.Net;
using CommunityToolkit.Maui.Core.Extensions;
using Contabee.Api.abstractions;
using ContaBeeMovil.Models;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServiceProvider _serviceProvider;

    public ServicioSesion(AppState appState, IServicioCrm servicioCrm, IServicioIdentidad servicioIdentidad, IServicioAlmacenamiento almacenamiento, IServiceProvider serviceProvider)
    {
        _appState = appState;
        _servicioCrm = servicioCrm;
        _servicioIdentidad = servicioIdentidad;
        _almacenamiento = almacenamiento;
        _serviceProvider = serviceProvider;
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

    private async Task ForzarReloginAsync(string mensaje)
    {
        await LimpiaTokensAsync();
        _appState.Perfil = null;
        _appState.CuentasFiscales = null;
        _appState.CuentaFiscalActual = null;
        _appState.MisUsuarios = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            var paginaLogin = _serviceProvider.GetRequiredService<PaginaLogin>();

            Application.Current!.Windows[0].Page = paginaLogin;
            await toastService.ShowAsync(mensaje, ToastType.Warning, 4000, ToastPosition.Top);
        });
    }

    public async Task GetPerfilAsync()
    {
        var respuesta = await _servicioIdentidad.GetPerfil();
        if (respuesta.Ok)
        {
            _appState.Perfil = respuesta.Payload;
            return;
        }

        await ForzarReloginAsync(respuesta.HttpCode == HttpStatusCode.Unauthorized
            ? "Tu sesión ha expirado, por favor inicia sesión nuevamente"
            : "Ocurrió un problema al obtener tu perfil");
    }

    public async Task GetLicenciaAsync()
    {
        if (_appState.CuentaFiscalActual is null)
        {
            _appState.Licenciamiento = new Contabee.Api.Crm.DtoLicenciamiento2
            {
                CuentaFiscalId = Guid.Empty,
                Ano = DateTime.Now.Year,
                Mes = DateTime.Now.Month,
                CreditosCaptura = 0,
                CreditosCapturaConsumo = 0,
                LicenciasCaptura = 0,
                LicenciasColaboracion = 0,
                CapturaOnPremise = false,
                ComprobacionesActivas = false,
                DevolucionesActivas = false,
            };
            return;
        }
       

        var respuesta = await _servicioCrm.GetLicenciamiento(_appState.CuentaFiscalActual.CuentaFiscalId);

        if (respuesta.Ok)
        {
            _appState.Licenciamiento = respuesta.Payload;
            return;
        }

        if (respuesta.HttpCode == HttpStatusCode.NotFound)
        {
            _appState.Licenciamiento = new Contabee.Api.Crm.DtoLicenciamiento2
            {
                CuentaFiscalId        = _appState.CuentaFiscalActual.CuentaFiscalId,
                Ano                   = DateTime.Now.Year,
                Mes                   = DateTime.Now.Month,
                CreditosCaptura       = 0,
                CreditosCapturaConsumo = 0,
                LicenciasCaptura      = 0,
                LicenciasColaboracion = 0,
                CapturaOnPremise      = false,
                ComprobacionesActivas = false,
                DevolucionesActivas   = false,
            };
            return;
        }

        await ForzarReloginAsync(respuesta.HttpCode == HttpStatusCode.Unauthorized
            ? "Tu sesión ha expirado, por favor inicia sesión nuevamente"
            : "Ocurrió un problema al obtener tu licencia");
    }

    public async Task GetAsociacionesFiscalesAsync()
    {
        var respuesta = await _servicioCrm.GetAsociacionesFiscales();

        if (respuesta.HttpCode == HttpStatusCode.NotFound)
        {
            _appState.CuentasFiscales = [];
            _appState.CuentaFiscalActual = null;
            return;
        }

        if (!respuesta.Ok)
        {
            await ForzarReloginAsync(respuesta.HttpCode == HttpStatusCode.Unauthorized
                ? "Tu sesión ha expirado, por favor inicia sesión nuevamente"
                : "Ocurrió un problema al cargar tus cuentas fiscales");
            return;
        }

        var cuentas = respuesta.Payload ?? [];
        _appState.CuentasFiscales = cuentas;

        var actualId = _appState.CuentaFiscalActual?.CuentaFiscalId;
        var estaEnLista = actualId.HasValue && cuentas.Any(c => c.CuentaFiscalId == actualId.Value);

        if (!estaEnLista)
        {
            _appState.CuentaFiscalActual = cuentas.First();
        }

        await GetMisUsuariosAsync();
    }

    public async Task GetMisUsuariosAsync()
    {
        var cfid = _appState.CuentaFiscalActual?.CuentaFiscalId;
        if (cfid.HasValue)
        {
            var usuarios = await _servicioIdentidad.MisUsuarios(cfid.Value);
            _appState.MisUsuarios = usuarios.Ok ? usuarios.Payload : [];
        }
    }

    public async Task GetTarjetasAsync()
    {
        var email = await LeeEmailAsync();
        if (string.IsNullOrEmpty(email)) return;

        var usuario = email.Split('@')[0];
        var clave = $"CLAVE_{usuario}";

        try
        {
            _appState.Tarjetas = await _almacenamiento.LeerSeguroAsync<List<TarjetaModel>>(clave) ?? [];
        }
        catch
        {
            _appState.Tarjetas = [];
            var toastService = _serviceProvider.GetRequiredService<IToastService>();
            await MainThread.InvokeOnMainThreadAsync(() =>
                toastService.ShowAsync("No se pudieron cargar tus tarjetas", ToastType.Warning, 4000, ToastPosition.Top));
        }
    }

    public async Task GuardarTarjetasAsync(List<TarjetaModel> tarjetas)
    {
        var email = await LeeEmailAsync();
        if (string.IsNullOrEmpty(email)) return;

        var usuario = email.Split('@')[0];
        var clave = $"CLAVE_{usuario}";

        await _almacenamiento.GuardarSeguroAsync(clave, tarjetas);
        _appState.Tarjetas = [.. tarjetas];
    }

    public async Task PosLoginAsync()
    {
        await GetPerfilAsync();
        await GetAsociacionesFiscalesAsync();
        await GetTarjetasAsync();
        await GetLicenciaAsync();
    }

    public async Task CerrarSesionAsync()
    {
        await LimpiaTokensAsync();
        SecureStorage.Remove(CLAVE_EXPIRACION);

        _appState.Perfil = null;
        _appState.CuentasFiscales = null;
        _appState.CuentaFiscalActual = null;
        _appState.Licenciamiento = null;
        _appState.MisUsuarios = null;
        _appState.Tarjetas = [];


        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var paginaLogin = _serviceProvider.GetRequiredService<PaginaLogin>();
            Application.Current!.Windows[0].Page = paginaLogin;
        });
    }
}
