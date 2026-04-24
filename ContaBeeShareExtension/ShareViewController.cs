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
    private const string LogFile = "shareext_log.txt";

    private string? _logPath;

    private void InitLog()
    {
        try
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
            if (container?.Path is not string p) return;
            _logPath = System.IO.Path.Combine(p, LogFile);
            if (System.IO.File.Exists(_logPath)) System.IO.File.Delete(_logPath);
            AppendLog("--- extension start ---");
        }
        catch { }
    }

    private void AppendLog(string msg)
    {
        try
        {
            if (_logPath == null) return;
            System.IO.File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        InitLog();
        AppendLog("ViewDidLoad");
        ProcessSharedImage();
    }

    private void ProcessSharedImage()
    {
        var inputItems = ExtensionContext?.InputItems;
        if (inputItems == null || inputItems.Length == 0)
        {
            AppendLog("CANCEL: no inputItems");
            Cancel(); return;
        }

        var attachments = inputItems[0].Attachments;
        if (attachments == null || attachments.Length == 0)
        {
            AppendLog("CANCEL: no attachments");
            Cancel(); return;
        }

        AppendLog($"attachments={attachments.Length}");
        foreach (var a in attachments)
            AppendLog($"  types: {string.Join(", ", a.RegisteredTypeIdentifiers ?? [])}");

        var imageProvider = attachments.FirstOrDefault(p => p.HasItemConformingTo(UtiImage));
        if (imageProvider == null)
        {
            AppendLog("CANCEL: no image attachment");
            Cancel(); return;
        }

        var hasPng  = imageProvider.HasItemConformingTo(UtiPng);
        var hasJpeg = imageProvider.HasItemConformingTo(UtiJpeg);
        AppendLog($"hasPng={hasPng} hasJpeg={hasJpeg}");

        var typeIdentifier = hasPng ? UtiPng : hasJpeg ? UtiJpeg : UtiImage;
        var ext      = typeIdentifier == UtiPng ? "png" : "jpg";
        var fileName = $"shared_{DateTime.Now:yyyyMMddHHmmss}.{ext}";
        AppendLog($"typeId={typeIdentifier} fileName={fileName}");

        var groupContainer = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        if (groupContainer == null)
        {
            AppendLog("CANCEL: groupContainer null");
            Cancel(); return;
        }

        var destUrl  = groupContainer.Append(fileName, isDirectory: false);
        var destPath = destUrl.Path;
        if (destPath == null)
        {
            AppendLog("CANCEL: destPath null");
            Cancel(); return;
        }
        AppendLog($"dest={destPath}");

        AppendLog("calling LoadDataRepresentation...");
        try
        {
            imageProvider.LoadDataRepresentation(typeIdentifier, (data, error) =>
            {
                AppendLog($"callback: data={data?.Length.ToString() ?? "null"} error={error?.LocalizedDescription ?? "null"}");

                if (error != null || data == null)
                {
                    AppendLog($"CANCEL: load failed error={error?.LocalizedDescription}");
                    InvokeOnMainThread(Cancel); return;
                }

                try
                {
                    var written = data.Save(destPath, atomically: true);
                    AppendLog($"Save={written}");

                    if (!written)
                    {
                        AppendLog("CANCEL: Save=false");
                        InvokeOnMainThread(Cancel); return;
                    }

                    var defaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
                    defaults.SetString(fileName, "pendingSharedImage");
                    defaults.Synchronize();
                    AppendLog("NSUserDefaults saved");

                    InvokeOnMainThread(() =>
                    {
                        // Intentar abrir la app principal. En iOS 17+ esto puede devolver false
                        // para custom URL schemes invocados desde una extensión de otra app (Photos).
                        // Completamos la extensión independientemente del resultado.
                        try
                        {
                            var url = new NSUrl(AppUrlScheme);
                            ExtensionContext?.OpenUrl(url, (success) =>
                            {
                                AppendLog($"OpenUrl success={success}");
                                // El completion handler llega en hilo de fondo — volver al main thread
                                InvokeOnMainThread(CompleteExtension);
                            });
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"OpenUrl ex={ex.Message}");
                            CompleteExtension();
                        }
                    });
                }
                catch (Exception ex)
                {
                    AppendLog($"CANCEL: save ex={ex.Message}");
                    InvokeOnMainThread(Cancel);
                }
            });
        }
        catch (Exception ex)
        {
            AppendLog($"CANCEL: LoadDataRepresentation threw={ex.Message}");
            Cancel();
        }
    }

    private void CompleteExtension()
    {
        try
        {
            ExtensionContext?.CompleteRequest([], null);
            AppendLog("DONE");
        }
        catch (Exception ex)
        {
            AppendLog($"CompleteRequest ex={ex.Message}");
            Cancel();
        }
    }

    private void Cancel()
    {
        AppendLog("CancelRequest");
        ExtensionContext?.CancelRequest(new NSError(new NSString("ContaBeeShareExtension"), 0));
    }
}
