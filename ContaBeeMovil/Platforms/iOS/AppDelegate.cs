using ContaBeeMovil.Helpers;
using Foundation;
using UIKit;
using System.IO;

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
        System.Diagnostics.Debug.WriteLine($"🍎 OpenUrl: {url.AbsoluteString}");

        if (url.AbsoluteString.StartsWith("contabee://shared-image"))
        {
            HandleSharedImageUrl();
            return true;
        }

        DeepLinkHandler.HandleDeepLink(url.AbsoluteString);
        return true;
    }

    private static void HandleSharedImageUrl()
    {
        try
        {
            const string appGroupId = "group.mx.contabee.app";
            var defaults = new NSUserDefaults(appGroupId, NSUserDefaultsType.SuiteName);
            var fileName = defaults.StringForKey("pendingSharedImage");

            if (string.IsNullOrEmpty(fileName)) return;

            var groupContainer = NSFileManager.DefaultManager.GetContainerUrl(appGroupId);
            if (groupContainer == null) return;

            var srcPath = Path.Combine(groupContainer.Path, fileName);
            var destPath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, fileName);

            if (File.Exists(srcPath))
            {
                File.Copy(srcPath, destPath, overwrite: true);
                defaults.RemoveObject("pendingSharedImage");
                System.Diagnostics.Debug.WriteLine($"📷 iOS share: imagen copiada a {destPath}");
                SharedImageHandler.HandleSharedImage(fileName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ iOS share: error — {ex.Message}");
        }
    }
}
