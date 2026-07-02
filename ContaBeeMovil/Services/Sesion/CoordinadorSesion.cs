using System.Net;
using ContaBeeMovil.Pages.Login;
using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Device;
using ContaBeeMovil.Services.Notifications;
using ContaBeeMovil.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ContaBeeMovil.Services.Sesion;

public interface ICoordinadorSesion
{
    // Único punto que asigna la página raíz de la ventana.
    Task NavegarAsync(DestinoNavegacion destino);

    // Único punto de terminación de sesión: decide política de token loginless y
    // destino de navegación a partir del motivo (regla de oro del sistema de sesión).
    // Devuelve true si terminó la sesión (limpió y navegó); false si la conservó
    // (p.ej. loginless ante error transitorio/sin red — no expulsa).
    Task<bool> CerrarSesionAsync(MotivoCierre motivo);

    // Entrada única desde AuthHandler para respuestas no-exitosas (403 / desvinculación).
    Task ManejarRespuestaAsync(HttpStatusCode code, string body, string path);
}

public class CoordinadorSesion : ICoordinadorSesion
{
    private readonly IServicioSesion _sesion;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppState _appState;
    private readonly IServicioLogs _logs;

    public CoordinadorSesion(IServicioSesion sesion, IServiceProvider serviceProvider, AppState appState, IServicioLogs logs)
    {
        _sesion = sesion;
        _serviceProvider = serviceProvider;
        _appState = appState;
        _logs = logs;
    }

    public Task NavegarAsync(DestinoNavegacion destino)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            Page page = destino switch
            {
                DestinoNavegacion.AppShell  => _serviceProvider.GetRequiredService<AppShell>(),
                DestinoNavegacion.Cargando  => new Pages.PaginaCargando(),
                _                           => new NavigationPage(_serviceProvider.GetRequiredService<PaginaLogin>())
            };
            Application.Current!.Windows[0].Page = page;
        });
    }

    public async Task<bool> CerrarSesionAsync(MotivoCierre motivo)
    {
        bool esLoginLess = !string.IsNullOrEmpty(await _sesion.LeeTokenLoginLessAsync());

        // Loginless resiliente: un error transitorio o la falta de red NUNCA borran el
        // token ni expulsan al usuario — solo avisamos y conservamos el estado para
        // reintentar. (Regla de oro: el token loginless solo se borra por acción explícita,
        // login de otra cuenta, o rechazo definitivo del backend.)
        if (esLoginLess && motivo is MotivoCierre.Transitorio or MotivoCierre.SinRed)
        {
            await MostrarToastAsync(motivo == MotivoCierre.SinRed
                ? "Sin conexión. Reintentaremos cuando vuelva la red."
                : "Problema temporal al cargar tus datos. Reintenta en un momento.");
            return false;
        }

        // Toda terminación real borra los tokens (incl. loginless) y va a Login. La
        // desactivación de asociación ya NO termina sesión: se maneja como recarga en modo
        // limitado (ver ServicioSesion.ProcesarDesvinculacionAsync).
        FiltrosDevolucionesView.LimpiarEstadoPersistido();
        FiltrosComprobacionesView.LimpiarEstadoPersistido();

        await _sesion.LimpiaTokensAsync(conservarLoginLess: false); // ya remueve la expiración

        _appState.Perfil = null;
        _appState.CuentasFiscales = null;
        _appState.CuentaFiscalActual = null;
        _appState.Licenciamiento = null;
        _appState.MisUsuarios = null;
        _appState.Tarjetas = [];

        await NavegarAsync(DestinoNavegacion.Login);

        var mensaje = MensajePara(motivo);
        if (mensaje is not null)
            await MostrarToastAsync(mensaje);

        return true;
    }

    public Task ManejarRespuestaAsync(HttpStatusCode code, string body, string path)
    {
        // Aquí SÍ tenemos el body real (aunque NSwag se lo trague en la capa de página), así
        // que detectamos por texto y NO por status: un 403 puede ser también "sin-permiso"
        // (rol restringido en una cuenta ajena a la que el usuario está vinculado), y ese NO
        // debe disparar el flujo de desvinculación. Solo un 403 cuyo body indica asociación
        // inactiva/eliminada recarga el estado; el body distingue además desactivada vs
        // eliminada para el mensaje. La detección por status queda como fallback en los catch
        // de las páginas, donde el body llega vacío.
        if (!CodigosErrorApi.EsAsociacionInactiva(body))
            return Task.CompletedTask;

        var tipo = CodigosErrorApi.ClasificarAccesoPerdido(body);
        _logs.Warn($"[Coordinador] Asociación inactiva ({(int)code}, {tipo}) — {path}");
        return _sesion.ManejarDesvinculacionAsync(tipo);
    }

    private static string? MensajePara(MotivoCierre motivo) => motivo switch
    {
        MotivoCierre.TokenRevocado      => "Tu sesión ha expirado, por favor inicia sesión nuevamente",
        MotivoCierre.ExpiradoSinRefresh => "Tu sesión ha expirado, por favor inicia sesión nuevamente",
        _                               => null // LogoutManual / CuentaEliminada / LoginOtraCuenta / VolverALogin: sin toast
    };

    private async Task MostrarToastAsync(string mensaje)
    {
        var toast = _serviceProvider.GetRequiredService<IServicioToast>();
        await MainThread.InvokeOnMainThreadAsync(() =>
            toast.MostrarAsync(mensaje, ToastIcono.Warning, ToastPosicion.Bottom));
    }
}
