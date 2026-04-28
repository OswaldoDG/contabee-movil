using ContaBeeMovil.Helpers;
using Foundation;
using UIKit;
using UserNotifications;

namespace ContaBeeMovil;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // App cerrada → abierta vía URL scheme (cold start)
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Solicitar permiso de notificaciones — necesario para que la Share Extension
        // pueda enviar la notificación "foto lista" cuando openURL falla en iOS 17+
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge,
            (_, _) => { });

        if (launchOptions != null)
        {
            var urlKey = UIApplication.LaunchOptionsUrlKey;
            if (launchOptions.TryGetValue(urlKey, out var urlValue) && urlValue is NSUrl launchUrl)
            {
                var uri = launchUrl.AbsoluteString;

                if (uri.StartsWith("contabee://shared-image"))
                    SharedImageHandler.NotifySharedImageScheme();
                else
                    DeepLinkHandler.HandleDeepLink(uri);
            }
        }

        return result;
    }

    // App en background → foreground vía URL scheme
    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        var uri = url.AbsoluteString;

        if (uri.StartsWith("contabee://shared-image"))
        {
            // Solo señalar — el acceso al App Group ocurre diferido en SharedImageHandler
            SharedImageHandler.NotifySharedImageScheme();
            return true;
        }

        DeepLinkHandler.HandleDeepLink(uri);
        return true;
    }
}
