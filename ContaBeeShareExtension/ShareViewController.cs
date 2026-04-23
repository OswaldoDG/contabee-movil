using Foundation;
using UIKit;

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
        if (inputItems == null || inputItems.Length == 0) { Cancel(); return; }

        var attachments = inputItems[0].Attachments;
        if (attachments == null || attachments.Length == 0) { Cancel(); return; }

        var imageProvider = attachments.FirstOrDefault(p => p.HasItemConformingTo(UtiImage));
        if (imageProvider == null) { Cancel(); return; }

        var typeIdentifier = imageProvider.HasItemConformingTo(UtiPng) ? UtiPng : UtiJpeg;

        imageProvider.LoadItem(typeIdentifier, null, (item, error) =>
        {
            if (error != null || item == null) { InvokeOnMainThread(Cancel); return; }

            try
            {
                var ext = typeIdentifier == UtiPng ? "png" : "jpg";
                var fileName = $"shared_{DateTime.Now:yyyyMMddHHmmss}.{ext}";

                var groupContainer = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
                if (groupContainer == null) { InvokeOnMainThread(Cancel); return; }

                var destUrl = groupContainer.Append(fileName, isDirectory: false);

                var sourceUrl = item as NSUrl;
                if (sourceUrl == null) { InvokeOnMainThread(Cancel); return; }

                NSFileManager.DefaultManager.Copy(sourceUrl, destUrl, out var copyError);
                if (copyError != null) { InvokeOnMainThread(Cancel); return; }

                var defaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
                defaults.SetString(fileName, "pendingSharedImage");
                defaults.Synchronize();

                InvokeOnMainThread(() =>
                {
                    var url = new NSUrl(AppUrlScheme);
                    ExtensionContext?.OpenUrl(url, _ =>
                    {
                        ExtensionContext?.CompleteRequest(Array.Empty<NSExtensionItem>(), null);
                    });
                });
            }
            catch
            {
                InvokeOnMainThread(Cancel);
            }
        });
    }

    private void Cancel()
    {
        ExtensionContext?.CancelRequest(new NSError(new NSString("ContaBeeShareExtension"), 0));
    }
}
