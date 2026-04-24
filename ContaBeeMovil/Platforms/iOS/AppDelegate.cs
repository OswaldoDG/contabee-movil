using ContaBeeMovil.Helpers;
using Foundation;
using UIKit;

namespace ContaBeeMovil;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // App cerrada → abierta vía URL scheme (cold start)
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        if (launchOptions != null)
        {
            var urlKey = UIApplication.LaunchOptionsUrlKey;
            if (launchOptions.TryGetValue(urlKey, out var urlValue) && urlValue is NSUrl launchUrl)
            {
                var uri = launchUrl.AbsoluteString;
                System.Diagnostics.Debug.WriteLine($"🍎 FinishedLaunching URL: {uri}");

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
        System.Diagnostics.Debug.WriteLine($"🍎 OpenUrl: {uri}");

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
