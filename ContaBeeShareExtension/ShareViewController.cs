using Foundation;
using UIKit;
using UserNotifications;

namespace ContaBeeShareExtension;

[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string AppGroupId = "group.mx.contabee.app";
    private const string AppUrlScheme = "contabee://shared-image";
    private const string UtiImage = "public.image";
    private const string UtiPng = "public.png";
    private const string UtiJpeg = "public.jpeg";

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        ProcessSharedImage();
    }

    private void ProcessSharedImage()
    {
        var inputItems = ExtensionContext?.InputItems;
        if (inputItems == null || inputItems.Length == 0)
        {
            Cancel(); return;
        }

        var attachments = inputItems[0].Attachments;
        if (attachments == null || attachments.Length == 0)
        {
            Cancel(); return;
        }

        var imageProvider = attachments.FirstOrDefault(p => p.HasItemConformingTo(UtiImage));
        if (imageProvider == null)
        {
            Cancel(); return;
        }

        var hasPng  = imageProvider.HasItemConformingTo(UtiPng);
        var hasJpeg = imageProvider.HasItemConformingTo(UtiJpeg);

        var typeIdentifier = hasPng ? UtiPng : hasJpeg ? UtiJpeg : UtiImage;
        var ext      = typeIdentifier == UtiPng ? "png" : "jpg";
        var fileName = $"shared_{DateTime.Now:yyyyMMddHHmmss}.{ext}";

        var groupContainer = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        if (groupContainer == null)
        {
            Cancel(); return;
        }

        var destUrl  = groupContainer.Append(fileName, isDirectory: false);
        var destPath = destUrl.Path;
        if (destPath == null)
        {
            Cancel(); return;
        }

        try
        {
            imageProvider.LoadDataRepresentation(typeIdentifier, (data, error) =>
            {
                if (error != null || data == null)
                {
                    InvokeOnMainThread(Cancel); return;
                }

                try
                {
                    var written = data.Save(destPath, atomically: true);

                    if (!written)
                    {
                        InvokeOnMainThread(Cancel); return;
                    }

                    var defaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
                    defaults.SetString(fileName, "pendingSharedImage");
                    defaults.Synchronize();
                    ScheduleNotification();

                    InvokeOnMainThread(() =>
                    {
                        var url = new NSUrl(AppUrlScheme);

                        // UIApplication.SharedApplication en el contexto de una Share Extension
                        // tiene acceso al UIApplication del host (Photos), que sí puede abrir
                        // URLs hacia otras apps — igual que WhatsApp/Gmail.
                        // ExtensionContext.OpenUrl devuelve false en iOS 17+ para custom schemes.
                        try
                        {
                            UIApplication.SharedApplication.OpenUrl(
                                url,
                                new NSDictionary<NSString, NSObject>(),
                                (_) => InvokeOnMainThread(CompleteExtension));
                        }
                        catch
                        {
                            // Fallback: ExtensionContext (puede devolver false pero intentamos)
                            try
                            {
                                ExtensionContext?.OpenUrl(url, (_) => InvokeOnMainThread(CompleteExtension));
                            }
                            catch
                            {
                                CompleteExtension();
                            }
                        }
                    });
                }
                catch
                {
                    InvokeOnMainThread(Cancel);
                }
            });
        }
        catch
        {
            Cancel();
        }
    }

    private void ScheduleNotification()
    {
        try
        {
            var content = new UNMutableNotificationContent
            {
                Title = "ContaBee",
                Body = "Tu foto está lista. Toca para procesarla.",
                Sound = UNNotificationSound.Default
            };
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, repeats: false);
            var request = UNNotificationRequest.FromIdentifier(
                $"shareext_{DateTime.Now:yyyyMMddHHmmss}", content, trigger);
            UNUserNotificationCenter.Current.AddNotificationRequest(request, (_) => { });
        }
        catch { }
    }

    private void CompleteExtension()
    {
        try
        {
            ExtensionContext?.CompleteRequest([], null);
        }
        catch
        {
            Cancel();
        }
    }

    private void Cancel()
    {
        ExtensionContext?.CancelRequest(new NSError(new NSString("ContaBeeShareExtension"), 0));
    }
}
