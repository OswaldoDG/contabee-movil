using ContaBeeMovil.Pages.Confirmar;
using ContaBeeMovil.Pages.RecuperarPass;

namespace ContaBeeMovil.Helpers;

public static class DeepLinkHandler
{
    private static PendingLink? _pendingDeepLink;
    private static bool _appReady = false;

    public static void HandleDeepLink(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return;

#if DEBUG
        System.Diagnostics.Debug.WriteLine($"🔗 Procesando: {uri}");
#endif

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)) return;

        var segments = parsedUri.AbsolutePath.Trim('/').Split('/');

        PendingLink? link = segments switch
        {
            ["cuenta", "confirmar", var token] => new PendingLink(TipoLink.ConfirmarCuenta, token),
            ["contrasena", "recuperar", var token] => new PendingLink(TipoLink.RecuperarContrasena, token),
            _ => null
        };

        if (link == null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"⚠️ Ruta no reconocida: {parsedUri.AbsolutePath}");
#endif
            return;
        }

        if (_appReady)
            Navegar(link);
        else
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"⏳ App no lista, guardando link...");
#endif
            _pendingDeepLink = link;
        }
    }

    public static void NotifyAppReady()
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"✅ App lista!");
#endif
        _appReady = true;

        if (_pendingDeepLink != null)
        {
            var link = _pendingDeepLink;
            _pendingDeepLink = null;
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"▶️ Procesando link pendiente...");
#endif
            Navegar(link);
        }
    }

    private static void Navegar(PendingLink link)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Obtiene la página según el tipo de link
                Page? page = link.Tipo switch
                {
                    TipoLink.ConfirmarCuenta => CrearPagina<ConfirmarCuentaPage>(
                        $"cuenta/confirmar?token={link.Token}"),
                    TipoLink.RecuperarContrasena => CrearPagina<RestablecerContrasenaPage>(
                        $"contrasena/recuperar?token={link.Token}"),
                    _ => null
                };

                if (page == null) return;

                Application.Current!.Windows[0].Page = page;
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"❌ Error navegando: {ex.Message}");
#endif
            }
        });
    }

    private static Page CrearPagina<T>(string parametros) where T : Page
    {
        // Obtiene la página con inyección de dependencias
        var page = App.Services.GetRequiredService<T>();

        // Si tiene QueryProperty, le pasamos los parámetros manualmente
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

    // Modelos internos
    private record PendingLink(TipoLink Tipo, string Token);

    private enum TipoLink
    {
        ConfirmarCuenta,
        RecuperarContrasena,
    }
}

//public static class DeepLinkHandler
//{
//    private static string? _pendingDeepLink;
//    private static bool _shellReady = false;

//    // La Shell se suscribe a este evento
//    public static event Action<string>? OnNavigate;

//    public static void HandleDeepLink(string? uri)
//    {
//        if (string.IsNullOrWhiteSpace(uri)) return;

//        System.Diagnostics.Debug.WriteLine($"🔗 Procesando: {uri}");

//        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)) return;

//        var segments = parsedUri.AbsolutePath
//                                .Trim('/')
//                                .Split('/');

//        string? route = segments switch
//        {
//            ["cuenta", "confirmar", var token] => $"/cuenta/confirmar?token={token}",
//            _ => null
//        };

//        if (route == null)
//        {
//            System.Diagnostics.Debug.WriteLine($"⚠️ Ruta no reconocida: {parsedUri.AbsolutePath}");
//            return;
//        }

//        if (_shellReady)
//            NavigateTo(route);
//        else
//        {
//            System.Diagnostics.Debug.WriteLine($"⏳ Shell no lista, guardando link...");
//            _pendingDeepLink = route;
//        }
//    }

//    // Llama este cuando la Shell esté lista
//    public static void NotifyShellReady()
//    {
//        System.Diagnostics.Debug.WriteLine($"✅ Shell lista!");
//        _shellReady = true;

//        if (_pendingDeepLink != null)
//        {
//            var route = _pendingDeepLink;
//            _pendingDeepLink = null;
//            System.Diagnostics.Debug.WriteLine($"▶️ Procesando link pendiente: {route}");
//            NavigateTo(route);
//        }
//    }

//    private static void NavigateTo(string route)
//    {
//        MainThread.BeginInvokeOnMainThread(async () =>
//        {
//            try
//            {
//                await Shell.Current.GoToAsync(route);
//            }
//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"❌ Error navegando: {ex.Message}");
//            }
//        });
//    }
//}