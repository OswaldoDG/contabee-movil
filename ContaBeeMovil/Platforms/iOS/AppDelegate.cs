using ContaBeeMovil.Helpers;
using Foundation;
using UIKit;

namespace ContaBeeMovil;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    // Captura el deep link cuando la app estaba CERRADA
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        if (launchOptions != null)
        {
            var urlKey = UIApplication.LaunchOptionsUrlKey;
            if (launchOptions.TryGetValue(urlKey, out var urlValue))
            {
                var url = urlValue as NSUrl;
                if (url != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🍎 Deep link (app cerrada): {url.AbsoluteString}");
                    DeepLinkHandler.HandleDeepLink(url.AbsoluteString);
                }
            }
        }

        return result;
    }

    // Captura el deep link cuando la app estaba ABIERTA
    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        System.Diagnostics.Debug.WriteLine($"🍎 Deep link (app abierta): {url.AbsoluteString}");
        DeepLinkHandler.HandleDeepLink(url.AbsoluteString);
        return true;
    }
}
