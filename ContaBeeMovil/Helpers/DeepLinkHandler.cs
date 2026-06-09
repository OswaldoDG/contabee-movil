using Contabee.Api.Transcript;
using ContaBeeMovil.Pages.Captura;
using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.RecuperarPass;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Helpers;

public static class DeepLinkHandler
{
    private static PendingLink? _pendingDeepLink;
    private static bool _appReady = false;

    private static IServicioLogs? Logs => App.Services?.GetService<IServicioLogs>();

    public static void HandleDeepLink(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)) return;

        var segments = parsedUri.AbsolutePath.Trim('/').Split('/');

        PendingLink? link = segments switch
        {
            ["cuenta", "confirmar", var token] => new PendingLink(TipoLink.ConfirmarCuenta, token),
            ["contrasena", "recuperar", var token] => new PendingLink(TipoLink.RecuperarContrasena, token),
            ["captura", "ticket"] => new PendingLink(TipoLink.CapturaTicket, string.Empty),
            _ => null
        };

        if (link == null)
        {
            Logs?.Warn($"[DeepLink] Ruta no reconocida: {parsedUri.AbsolutePath}");
            return;
        }

        Logs?.Info($"[DeepLink] Link recibido — tipo={link.Tipo}");

        if (_appReady)
            Navegar(link);
        else
        {
            Logs?.Info("[DeepLink] App no lista, guardando link pendiente");
            _pendingDeepLink = link;
        }
    }

    public static void HandleWidgetCaptura()
    {
        Logs?.Info("[DeepLink] Widget: solicitud de apertura de PaginaCaptura");
        if (_appReady)
            NavegarACaptura();
        else
            _pendingDeepLink = new PendingLink(TipoLink.CapturaTicket, string.Empty);
    }

    public static void NotifyAppReady()
    {
        Logs?.Info("[DeepLink] App lista");
        _appReady = true;

        if (_pendingDeepLink != null)
        {
            var link = _pendingDeepLink;
            _pendingDeepLink = null;
            Logs?.Info($"[DeepLink] Procesando link pendiente — tipo={link.Tipo}");
            Navegar(link);
        }
    }

    private static void Navegar(PendingLink link)
    {
        if (link.Tipo == TipoLink.CapturaTicket)
        {
            NavegarACaptura();
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Page? page = link.Tipo switch
                {
                    TipoLink.ConfirmarCuenta => CrearPagina<ConfirmarCuentaPage>(
                        $"cuenta/confirmar?token={link.Token}"),
                    TipoLink.RecuperarContrasena => CrearPagina<RestablecerContrasenaPage>(
                        $"contrasena/recuperar?token={link.Token}"),
                    _ => null
                };

                if (page == null) return;

                Application.Current!.Windows[0].Page = page is ConfirmarCuentaPage
                    ? new NavigationPage(page)
                    : page;
            }
            catch (Exception ex)
            {
                Logs?.Error($"[DeepLink] Error al navegar: {ex.GetType().Name} - {ex.Message}");
            }
        });
    }

    private static void NavegarACaptura()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(300);
                var tieneSesion = Preferences.Get("TieneSesion", false);
                if (!tieneSesion)
                {
                    Logs?.Info("[DeepLink] Widget: sin sesión — se omite navegación");
                    return;
                }
                if (Shell.Current == null)
                {
                    Logs?.Warn("[DeepLink] Widget: Shell.Current es null");
                    return;
                }
                Logs?.Info("[DeepLink] Widget: navegando a PaginaCaptura");
                await Shell.Current.GoToAsync(nameof(PaginaCaptura),
                    new Dictionary<string, object>
                    {
                        ["tipo"] = TipoProcesoCaptura.FacturaIndividual
                    });
            }
            catch (Exception ex)
            {
                Logs?.Error($"[DeepLink] Widget: error — {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private static Page CrearPagina<T>(string parametros) where T : Page
    {
        var page = App.Services.GetRequiredService<T>();

        if (page is ConfirmarCuentaPage confirmar &&
            parametros.Contains("token="))
        {
            var token = parametros.Split("token=")[1];
            confirmar.Token = Uri.UnescapeDataString(token);
        }

        if (page is RestablecerContrasenaPage restablecer &&
            parametros.Contains("token="))
        {
            var token = parametros.Split("token=")[1];
            restablecer.Token = Uri.UnescapeDataString(token);
        }

        return page;
    }

    private record PendingLink(TipoLink Tipo, string Token);

    private enum TipoLink
    {
        ConfirmarCuenta,
        RecuperarContrasena,
        CapturaTicket,
    }
}
