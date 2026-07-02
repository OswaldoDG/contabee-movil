using System.Net;
using CommunityToolkit.Maui.Core.Extensions;
using Contabee.Api.abstractions;
using TarjetaDto = Contabee.Api.Crm.TarjetaUsuario;
using ContaBeeMovil.Models;
using ContaBeeMovil.Services.Almacenamiento;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Services.Sesion;
using ContaBeeMovil.Views;
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
    private const string CLAVE_TOKEN_LOGINLESS = "TokenLoginLess";
    // Espejo síncrono (Preferences) de la presencia del token loginless en
    // SecureStorage. Permite decidir la pantalla inicial en App.CreateWindow —que es
    // síncrono— sin tener que leer SecureStorage de forma asíncrona. Mismo patrón que
    // "TieneSesion".
    public const string CLAVE_TIENE_TOKEN_LOGINLESS = "TieneTokenLoginLess";
    private readonly AppState _appState;
    private readonly IServicioCrm _servicioCrm;
    private readonly IServicioIdentidad _servicioIdentidad;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private readonly IServiceProvider _serviceProvider;
    private readonly IServicioLogs _logs;
    private bool _posLoginAbortado;
    private static readonly SemaphoreSlim _desvinculacionLock = new(1, 1);
    private DateTime _ultimaDesvinculacion = DateTime.MinValue;
    private static readonly TimeSpan _cooldownDesvinculacion = TimeSpan.FromSeconds(30);

    // Resolución perezosa del coordinador para evitar un ciclo de DI en el constructor
    // (el coordinador depende de IServicioSesion). El coordinador es la ÚNICA autoridad
    // de navegación y terminación de sesión; ServicioSesion delega ahí toda decisión
    // terminal (limpiar tokens + a dónde ir).
    private ICoordinadorSesion? _coordinador;
    private ICoordinadorSesion Coordinador => _coordinador ??= _serviceProvider.GetRequiredService<ICoordinadorSesion>();

    public ServicioSesion(AppState appState, IServicioCrm servicioCrm, IServicioIdentidad servicioIdentidad, IServicioAlmacenamiento almacenamiento, IServiceProvider serviceProvider, IServicioLogs logs)
    {
        _appState = appState;
        _servicioCrm = servicioCrm;
        _servicioIdentidad = servicioIdentidad;
        _almacenamiento = almacenamiento;
        _serviceProvider = serviceProvider;
        _logs = logs;
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

    public Task LimpiaTokensAsync(bool conservarLoginLess = false)
    {
        SecureStorage.Remove(CLAVE_ACCESS_TOKEN);
        SecureStorage.Remove(CLAVE_REFRESH_TOKEN);
        SecureStorage.Remove(CLAVE_EXPIRACION);
        // Para un usuario loginless conservamos el token: la asociación pudo haber
        // sido solo DESACTIVADA (reversible) y el token sigue siendo válido tras
        // reactivar, así que lo necesitamos para reanudar la sesión automáticamente.
        if (!conservarLoginLess)
        {
            SecureStorage.Remove(CLAVE_TOKEN_LOGINLESS);
            Preferences.Set(CLAVE_TIENE_TOKEN_LOGINLESS, false);
            _appState.EsLoginLess = false;
        }
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

    // Aborta el post-login y delega la terminación (limpiar tokens + navegar) al
    // coordinador, que aplica la política de token loginless según el motivo. Si el
    // coordinador conserva la sesión (loginless ante error transitorio/sin red) NO se
    // marca abortado: la carga simplemente se detiene sin expulsar al usuario.
    private async Task AbortarYCerrarAsync(MotivoCierre motivo)
    {
        bool termino = await Coordinador.CerrarSesionAsync(motivo);
        if (termino) _posLoginAbortado = true;
    }

    // Mapea el código HTTP al motivo de cierre: 401 = token muerto tras agotar refresh;
    // cualquier otro error no-503 = transitorio (para loginless NO expulsa).
    private static MotivoCierre MotivoPorHttp(HttpStatusCode? code) =>
        code == HttpStatusCode.Unauthorized ? MotivoCierre.TokenRevocado : MotivoCierre.Transitorio;

    public async Task GetPerfilAsync()
    {
        var respuesta = await _servicioIdentidad.GetPerfil();
        if (respuesta.Ok)
        {
            _appState.Perfil = respuesta.Payload;
            return;
        }

        if (respuesta.HttpCode == HttpStatusCode.ServiceUnavailable)
            return;

        await AbortarYCerrarAsync(MotivoPorHttp(respuesta.HttpCode));
    }

    public async Task GetLicenciaAsync()
    {
        if (_appState.CuentaFiscalActual is null)
        {
            _appState.Licenciamiento = new Contabee.Api.Crm.DtoLicenciamiento2
            {
                CuentaFiscalId        = Guid.Empty,
                Ano                   = DateTime.Now.Year,
                Mes                   = DateTime.Now.Month,
                CreditosAdquiridos    = 0,
                CreditosDisponibles   = 0,
                CreditosCapturaConsumo = 0,
                LicenciasCaptura      = 0,
                LicenciasColaboracion = 0,
                CapturaOnPremise      = false,
                ComprobacionesActivas = false,
                DevolucionesActivas   = false,
            };
            return;
        }
       

        var cfid = _appState.CuentaFiscalActual.CuentaFiscalId;
        _logs.Log($"[Licencia] GET licenciamiento → cfid={cfid}");

        var respuesta = await _servicioCrm.GetLicenciamiento(cfid);

        if (respuesta.Ok)
        {
            var p = respuesta.Payload;
            _logs.Log($"[Licencia] OK → Disponibles={p?.CreditosDisponibles} Adquiridos={p?.CreditosAdquiridos} Consumo={p?.CreditosCapturaConsumo} LicCap={p?.LicenciasCaptura} LicColab={p?.LicenciasColaboracion}");
            _appState.Licenciamiento = p;
            return;
        }

        _logs.Log($"[Licencia] ERROR → HttpCode={respuesta.HttpCode} Mensaje={respuesta.Error?.Mensaje}");

        if (respuesta.HttpCode == HttpStatusCode.NotFound)
        {
            _appState.Licenciamiento = new Contabee.Api.Crm.DtoLicenciamiento2
            {
                CuentaFiscalId        = _appState.CuentaFiscalActual.CuentaFiscalId,
                Ano                   = DateTime.Now.Year,
                Mes                   = DateTime.Now.Month,
                CreditosAdquiridos    = 0,
                CreditosDisponibles   = 0,
                CreditosCapturaConsumo = 0,
                LicenciasCaptura      = 0,
                LicenciasColaboracion = 0,
                CapturaOnPremise      = false,
                ComprobacionesActivas = false,
                DevolucionesActivas   = false,
            };
            return;
        }

        if (respuesta.HttpCode == HttpStatusCode.ServiceUnavailable)
            return;

        await AbortarYCerrarAsync(MotivoPorHttp(respuesta.HttpCode));
    }

    public async Task GetAsociacionesFiscalesAsync()
    {
        _logs.Log("[CuentasFiscales] GET asociaciones fiscales");

        var respuesta = await _servicioCrm.GetAsociacionesFiscales();

        if (respuesta.Ok)
        {
            _logs.Log($"[CuentasFiscales] OK → {respuesta.Payload?.Count ?? 0} cuenta(s)");
            AplicarCuentasFiscales(respuesta.Payload ?? []);
            await GetMisUsuariosAsync();
            return;
        }

        // Sin internet — PaginaSinConexion ya mostrada por AuthHandler
        if (respuesta.HttpCode == HttpStatusCode.ServiceUnavailable)
            return;

        // Token expirado sin refresh → token muerto, terminar
        if (respuesta.HttpCode == HttpStatusCode.Unauthorized)
        {
            await AbortarYCerrarAsync(MotivoCierre.TokenRevocado);
            return;
        }

        // Error transitorio — reintentar una vez
        await Task.Delay(1500);
        respuesta = await _servicioCrm.GetAsociacionesFiscales();

        if (respuesta.Ok)
        {
            AplicarCuentasFiscales(respuesta.Payload ?? []);
            await GetMisUsuariosAsync();
            return;
        }

        if (respuesta.HttpCode == HttpStatusCode.ServiceUnavailable)
            return;

        _logs.Error($"[CuentasFiscales] ERROR → HttpCode={respuesta.HttpCode} Mensaje={respuesta.Error?.Mensaje}");

        await AbortarYCerrarAsync(MotivoPorHttp(respuesta.HttpCode));
    }

    public void AplicarCuentasFiscales(List<Contabee.Api.Crm.AsociacionCuentaFiscalCompleta> cuentas)
    {
        _appState.CuentasFiscales = cuentas;
        var actualId = _appState.CuentaFiscalActual?.CuentaFiscalId;
        var cuentaFresca = actualId.HasValue ? cuentas.FirstOrDefault(c => c.CuentaFiscalId == actualId.Value) : null;
        // Siempre actualizar con la versión fresca del servidor para reflejar cambios
        // como EstadoLicenciaDemo después de reclamar créditos demo.
        _appState.CuentaFiscalActual = cuentaFresca ?? cuentas.FirstOrDefault();
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

        var clave = $"CLAVE_{email.Split('@')[0]}";

        // Si hay cambios locales offline pendientes, subirlos antes de hacer fetch del servidor
        if (Preferences.Get("TarjetasPendienteSincronizacion", false))
        {
            var localesPendientes = await _almacenamiento.LeerSeguroAsync<List<TarjetaModel>>(clave);
            if (localesPendientes?.Count > 0)
            {
                var push = await _servicioCrm.GuardarMisTarjetasUsuario(localesPendientes.Select(ToDto).ToList());
                if (push.Ok) Preferences.Set("TarjetasPendienteSincronizacion", false);
            }
            else
            {
                Preferences.Set("TarjetasPendienteSincronizacion", false);
            }
        }

        var respuesta = await _servicioCrm.MisTarjetasUsuario();

        if (respuesta.Ok)
        {
            var tarjetas = respuesta.Payload?.Select(FromDto).ToList() ?? [];

            // Migración silenciosa: backend vacío pero hay datos locales → subir
            if (tarjetas.Count == 0)
            {
                var locales = await _almacenamiento.LeerSeguroAsync<List<TarjetaModel>>(clave);
                if (locales?.Count > 0)
                {
                    var migracion = await _servicioCrm.GuardarMisTarjetasUsuario(locales.Select(ToDto).ToList());
                    tarjetas = migracion.Ok ? locales : [];
                }
            }

            _appState.Tarjetas = tarjetas;
            await _almacenamiento.GuardarSeguroAsync(clave, tarjetas);
        }
        else
        {
            try { _appState.Tarjetas = await _almacenamiento.LeerSeguroAsync<List<TarjetaModel>>(clave) ?? []; }
            catch { _appState.Tarjetas = []; }

            var toast = _serviceProvider.GetRequiredService<IServicioToast>();
            await MainThread.InvokeOnMainThreadAsync(() =>
                toast.MostrarAsync("No se pudieron sincronizar tus tarjetas", ToastIcono.Warning, ToastPosicion.Bottom));
        }
    }

    public async Task GuardarTarjetasAsync(List<TarjetaModel> tarjetas)
    {
        var email = await LeeEmailAsync();
        if (string.IsNullOrEmpty(email)) return;

        var clave = $"CLAVE_{email.Split('@')[0]}";

        var respuesta = await _servicioCrm.GuardarMisTarjetasUsuario(tarjetas.Select(ToDto).ToList());
        if (respuesta.Ok)
        {
            Preferences.Set("TarjetasPendienteSincronizacion", false);
        }
        else
        {
            Preferences.Set("TarjetasPendienteSincronizacion", true);
            var toast = _serviceProvider.GetRequiredService<IServicioToast>();
            await MainThread.InvokeOnMainThreadAsync(() =>
                toast.MostrarAsync("No se pudieron guardar tus tarjetas en la nube", ToastIcono.Warning, ToastPosicion.Bottom));
        }

        await _almacenamiento.GuardarSeguroAsync(clave, tarjetas);
        _appState.Tarjetas = [.. tarjetas];
    }

    public async Task PosLoginAsync()
    {
        FiltrosDevolucionesView.LimpiarEstadoPersistido();
        FiltrosComprobacionesView.LimpiarEstadoPersistido();

        _posLoginAbortado = false;

        var tokenLoginLess = await LeeTokenLoginLessAsync();
        _appState.EsLoginLess = !string.IsNullOrEmpty(tokenLoginLess);
        // Mantener el espejo síncrono en sync (auto-sana instalaciones previas que
        // aún no tienen el flag persistido).
        Preferences.Set(CLAVE_TIENE_TOKEN_LOGINLESS, !string.IsNullOrEmpty(tokenLoginLess));

        await GetPerfilAsync();
        if (_posLoginAbortado) return;

        await GetAsociacionesFiscalesAsync();
        if (_posLoginAbortado) return;

        await GetTarjetasAsync();
        if (_posLoginAbortado) return;

        await GetLicenciaAsync();
    }

    public async Task VerificarSesionAlReanudarAsync()
    {
        if (!Preferences.Get("TieneSesion", false))
        {
            // Usuario loginless con acceso suspendido (asociación desactivada): el token
            // sigue válido, así que intentamos reanudar por si ya fue reactivada.
            if (!string.IsNullOrEmpty(await LeeTokenLoginLessAsync()))
                await IntentarReanudarLoginLessAsync();
            return;
        }

        await GetTarjetasAsync();

        bool esLoginLess = !string.IsNullOrEmpty(await LeeTokenLoginLessAsync());
        var expiracion = await LeeExpiracionAsync();
        bool tokenExpirado = expiracion.HasValue && DateTime.Now >= expiracion.Value;

        if (tokenExpirado)
        {
            bool puedeRefrescar = (esLoginLess || _appState.Recordarme) && !string.IsNullOrEmpty(await LeeRefreshTokenAsync());
            if (!puedeRefrescar)
            {
                // Sin red: no forzar logout al reanudar — esperar a que vuelva la conexión.
                var access = Connectivity.Current.NetworkAccess;
                if (access is not NetworkAccess.Internet and not NetworkAccess.ConstrainedInternet)
                    return;

                // Loginless sin refresh token (instalación legacy): en vez de expulsar,
                // re-reanudamos con el token loginless para obtener un refresh token nuevo
                // (self-heal, sin re-vincular). IntentarReanudar navega según el resultado.
                if (esLoginLess)
                {
                    await IntentarReanudarLoginLessAsync();
                    return;
                }

                // Normal sin Recordarme: expiración definitiva → cerrar sesión.
                await AbortarYCerrarAsync(MotivoCierre.ExpiradoSinRefresh);
                return;
            }
        }

        // La desactivación de asociación se detecta de forma reactiva (403 asociacion-inactiva
        // → ManejarDesvinculacionAsync) en la primera llamada al API tras reanudar. No hace
        // falta un chequeo proactivo aquí.

        // Recuperación de modo limitado: si el usuario está autenticado pero sin cuenta
        // fiscal activa (asociación desactivada), al reanudar re-consultamos por si el
        // primario ya la reactivó. Si es así, RefrescarAcceso recupera y reinicia solo.
        if (_appState.CuentaFiscalActual is null)
            await RefrescarAccesoAsync();
    }

    public async Task ManejarDesvinculacionAsync(TipoAccesoPerdido tipo = TipoAccesoPerdido.Desconocido)
    {
        if (!await _desvinculacionLock.WaitAsync(0)) return;
        try
        {
            // Cooldown: mientras el backend propaga el cambio de asociación (caché ~3 min),
            // los endpoints de datos siguen devolviendo 403. Sin esto recargaríamos en cada
            // 403. Solo reevaluamos una vez por ventana.
            if (DateTime.Now - _ultimaDesvinculacion < _cooldownDesvinculacion) return;
            _ultimaDesvinculacion = DateTime.Now;

            await ProcesarDesvinculacionAsync(tipo);
        }
        finally
        {
            _desvinculacionLock.Release();
        }
    }

    // Desactivación de asociación: el usuario SIGUE autenticado, solo perdió acceso a esa
    // cuenta fiscal. En vez de una pantalla bloqueante, hacemos un "reinicio ligero"
    // cubierto por PaginaCargando (para no exponer estados default/intermedios) y avisamos
    // con un toast al terminar.
    //   - Si quedan cuentas → AplicarCuentasFiscales selecciona la primera y carga sus datos.
    //   - Si NO quedan cuentas → CuentaFiscalActual = null → AppShell en modo limitado.
    private async Task ProcesarDesvinculacionAsync(TipoAccesoPerdido tipo)
    {
        // Overlay a pantalla completa DESDE la detección (no solo durante la recarga), para
        // que el usuario perciba de inmediato que la app está actualizándose.
        _appState.MostrarCargaGlobal = true;
        try
        {
            var idAntes = _appState.CuentaFiscalActual?.CuentaFiscalId;

            // Detección con la lista fresca. Si no se puede obtener, no hacemos nada.
            var respuesta = await _servicioCrm.GetAsociacionesFiscales();
            if (!respuesta.Ok) return;

            var cuentas = respuesta.Payload ?? [];
            bool cuentaSigueActiva = idAntes.HasValue && cuentas.Any(c => c.CuentaFiscalId == idAntes.Value);

            // Caché ~3 min: si la cuenta activa aún aparece, el backend no propagó el cambio.
            // No reiniciamos (evita bucles), pero avisamos con un toast —una vez por ventana
            // de cooldown—; cuando la lista fresca refleje el cambio, un 403 posterior recarga.
            if (cuentaSigueActiva)
            {
                _ = NotificarAsync(MensajeAccesoPerdido(tipo, limitado: false));
                return;
            }

            // La cuenta activa ya no está → recarga EN SITIO (sin pantalla negra).
            await RecargarCuentaEnSitioAsync(cuentas);
            if (_posLoginAbortado) return; // el token murió durante la recarga → ya se fue a Login

            _ = NotificarAsync(MensajeAccesoPerdido(tipo, limitado: _appState.CuentaFiscalActual is null));
        }
        finally
        {
            _appState.MostrarCargaGlobal = false; // garantiza que el overlay se apague
        }
    }

    // Mensaje según el motivo (desactivada vs eliminada). Si quedó en modo limitado (sin
    // ninguna cuenta), añade la aclaración de funciones limitadas.
    private static string MensajeAccesoPerdido(TipoAccesoPerdido tipo, bool limitado)
    {
        string baseMsg = tipo switch
        {
            TipoAccesoPerdido.Eliminada   => "Fuiste desvinculado de esta cuenta fiscal",
            TipoAccesoPerdido.Desactivada => "Tu acceso a esta cuenta fiscal fue desactivado",
            _                             => "Perdiste el acceso a esta cuenta fiscal"
        };
        return limitado ? $"{baseMsg}. Algunas funciones estarán limitadas." : baseMsg;
    }

    // Recarga la cuenta activa EN SITIO, sin pantalla negra: mismo patrón que el cambio de
    // cuenta del selector (spinner en la barra de RFC + invalidación de listados vía
    // EstaActualizandoCF). Selecciona la primera cuenta disponible —o null (modo limitado)
    // si ya no queda ninguna— y refresca licencia y usuarios en paralelo. No reconstruye el
    // AppShell (que es lo que causaba los ~3 s de pantalla en negro).
    private async Task RecargarCuentaEnSitioAsync(List<Contabee.Api.Crm.AsociacionCuentaFiscalCompleta> cuentas)
    {
        AplicarCuentasFiscales(cuentas); // primera-o-null → dispara las reacciones de UI
        _posLoginAbortado = false;
        _appState.MostrarCargaGlobal = true;  // overlay a pantalla completa (perceptible)
        _appState.EstaActualizandoCF = true;
        try
        {
            await Task.WhenAll(GetMisUsuariosAsync(), GetLicenciaAsync());
        }
        catch { /* errores transitorios no deben bloquear la recarga */ }
        _appState.EstaActualizandoCF = false; // MainTabbedPage invalida listados y recarga la pestaña
        _appState.MostrarCargaGlobal = false;
    }

    private Task NotificarAsync(string mensaje)
    {
        var toast = _serviceProvider.GetRequiredService<IServicioToast>();
        return MainThread.InvokeOnMainThreadAsync(() =>
            toast.MostrarAsync(mensaje, ToastIcono.Warning, ToastPosicion.Bottom));
    }

    // Recuperación de modo limitado: re-consulta asociaciones para ver si la cuenta fiscal
    // fue reactivada por el primario. Si vuelve a haber cuentas, recarga en sitio y devuelve
    // true; si sigue sin cuentas, devuelve false (el llamador da feedback). Usado por el
    // banner "Actualizar" y por el auto-rechequeo al reanudar la app.
    public async Task<bool> RefrescarAccesoAsync()
    {
        var respuesta = await _servicioCrm.GetAsociacionesFiscales();
        if (!respuesta.Ok) return false;

        var cuentas = respuesta.Payload ?? [];
        if (cuentas.Count == 0) return false; // sigue sin cuentas activas

        await RecargarCuentaEnSitioAsync(cuentas);
        return !_posLoginAbortado; // false si el token murió durante la recarga
    }

    public Task CerrarSesionAsync() => Coordinador.CerrarSesionAsync(MotivoCierre.LogoutManual);

    public async Task<bool> IntentarReanudarLoginLessAsync()
    {
        var token = await LeeTokenLoginLessAsync();
        if (string.IsNullOrEmpty(token)) return false;

        var dispositivoId = await LeeIdDeDispositivo();
        // recordarme: true → solicita el scope offline_access para obtener refresh token.
        // Un usuario loginless no tiene forma de reingresar credenciales, así que SIEMPRE
        // debe tener refresh token (igual que en el login loginless inicial). Sin esto, al
        // expirar el access token la sesión se cerraría sin poder refrescar.
        var loginR = await _servicioIdentidad.IniciarSesion(token, "Password", dispositivoId, recordarme: true);
        if (!loginR.Ok || loginR.Payload is null)
        {
            _logs.Warn($"[LoginLess] Reanudar acceso falló. Ok={loginR.Ok} Error={loginR.Error?.Codigo}");

            // Distinguir rechazo definitivo (token revocado) de fallo transitorio/sin red.
            // Solo con señal POSITIVA de revocación y CON red se borra el token; ante
            // cualquier ambigüedad se conserva (regla de oro — default seguro).
            var access = Connectivity.Current.NetworkAccess;
            bool conRed = access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
            bool revocado = conRed &&
                (loginR.Error?.Codigo?.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) ?? false);

            if (revocado)
                await Coordinador.CerrarSesionAsync(MotivoCierre.TokenRevocado);

            // Transitorio/sin red o desactivación reversible: se conserva el token; el
            // llamador decide (permanecer en acceso suspendido / cargando-offline).
            return false;
        }

        await GuardaTokenAsync(loginR.Payload.AccessToken, loginR.Payload.RefreshToken);
        await GuardaExpiracionAsync(DateTime.Now.AddSeconds(loginR.Payload.ExpiresIn));
        await PosLoginAsync();
        if (_posLoginAbortado) return false;

        await Coordinador.NavegarAsync(DestinoNavegacion.AppShell);

        // Se reanudó la sesión, pero puede seguir sin cuentas fiscales activas (la
        // asociación aún no ha sido reactivada). En ese caso el usuario entra en modo
        // limitado; le avisamos que su cuenta fiscal sigue inactiva.
        if (_appState.CuentaFiscalActual is null)
        {
            var toast = _serviceProvider.GetRequiredService<IServicioToast>();
            await toast.MostrarAsync(
                "Tu cuenta fiscal está inactiva. Algunas funciones estarán limitadas.",
                ToastIcono.Warning, ToastPosicion.Bottom);
        }

        return true;
    }

    public Task GuardaTokenLoginLessAsync(string token)
    {
        Preferences.Set(CLAVE_TIENE_TOKEN_LOGINLESS, true);
        return GuardaContenidoClave(CLAVE_TOKEN_LOGINLESS, token);
    }

    public Task<string?> LeeTokenLoginLessAsync()
        => LeeContenidoClave(CLAVE_TOKEN_LOGINLESS);

    // Borra SOLO el token loginless (sin tocar access/refresh). Se usa al iniciar sesión
    // con una cuenta completa: el token loginless previo del dispositivo queda obsoleto.
    public Task LimpiaTokenLoginLessAsync()
    {
        SecureStorage.Remove(CLAVE_TOKEN_LOGINLESS);
        Preferences.Set(CLAVE_TIENE_TOKEN_LOGINLESS, false);
        _appState.EsLoginLess = false;
        return Task.CompletedTask;
    }

    public async Task PostEliminarCuentaAsync()
    {
        FiltrosDevolucionesView.LimpiarEstadoPersistido();
        FiltrosComprobacionesView.LimpiarEstadoPersistido();

        // Borrar tarjetas del usuario del SecureStorage
        var email = await LeeEmailAsync();
        if (!string.IsNullOrEmpty(email))
        {
            var usuario = email.Split('@')[0];
            _almacenamiento.EliminarSeguro($"CLAVE_{usuario}");
        }

        // Borrar email del SecureStorage
        await LimpiaEmailAsync();

        // Borrar tokens (incl. loginless) + estado global + navegar a login.
        await Coordinador.CerrarSesionAsync(MotivoCierre.CuentaEliminada);
    }

    // ── Helpers de mapeo TarjetaModel ↔ TarjetaUsuario (DTO de API) ───────────

    private static TarjetaDto ToDto(TarjetaModel m) =>
        new() { Id = Guid.Parse(m.Id), Alias = m.Alias, UltimosDigitos = m.UltimosDigitos };

    private static TarjetaModel FromDto(TarjetaDto d) =>
        new() { Id = d.Id.ToString(), Alias = d.Alias ?? string.Empty, UltimosDigitos = d.UltimosDigitos ?? string.Empty };

}
