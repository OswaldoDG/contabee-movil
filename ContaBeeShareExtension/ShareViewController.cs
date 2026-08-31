using Foundation;
using ObjCRuntime;
using UIKit;
using UserNotifications;

namespace ContaBeeShareExtension;

[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string AppGroupId = "group.mx.contabee.app";
    private const string PendingKey = "pendingSharedImage";
    private const string UtiImage = "public.image";
    private const string UtiPng = "public.png";
    private const string UtiJpeg = "public.jpeg";
    private UILabel? _status;
    private UIButton? _done;
    private NSUrl? _groupContainer;

    // iOS crea el controlador desde NSExtensionPrincipalClass. Este constructor
    // únicamente enlaza la instancia nativa con la administrada; la inicialización
    // de la interfaz debe permanecer en ViewDidLoad.
    protected ShareViewController(NativeHandle handle) : base(handle)
    {
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        ConfigureView();
        Log("ViewDidLoad: inició la Share Extension");
        ProcessSharedImage();
    }

    private void ConfigureView()
    {
        View!.BackgroundColor = UIColor.SystemBackground;
        _status = new UILabel { Text = "Preparando imagen…", TextAlignment = UITextAlignment.Center,
            Lines = 0, TranslatesAutoresizingMaskIntoConstraints = false };
        _done = UIButton.FromType(UIButtonType.System);
        _done.SetTitle("Listo", UIControlState.Normal);
        _done.Hidden = true;
        _done.TranslatesAutoresizingMaskIntoConstraints = false;
        _done.TouchUpInside += (_, _) => CompleteExtension();
        View.AddSubviews(_status, _done);
        NSLayoutConstraint.ActivateConstraints([
            _status.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 24),
            _status.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -24),
            _status.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor, -18),
            _done.TopAnchor.ConstraintEqualTo(_status.BottomAnchor, 18),
            _done.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor)
        ]);
    }

    private void ProcessSharedImage()
    {
        var inputItems = ExtensionContext?.InputItems;
        Log($"InputItems: {inputItems?.Length ?? 0}");
        if (inputItems == null || inputItems.Length == 0) { Fail("No se recibió contenido para compartir."); return; }

        var attachments = inputItems.OfType<NSExtensionItem>()
            .SelectMany(item => item.Attachments ?? []).ToArray();
        Log($"Attachments: {attachments.Length}");
        foreach (var attachment in attachments)
            Log($"Tipos registrados: {string.Join(", ", attachment.RegisteredTypeIdentifiers)}");

        var provider = attachments.FirstOrDefault(p => p.HasItemConformingTo(UtiImage));
        if (provider == null) { Fail("El elemento compartido no contiene una imagen compatible."); return; }

        var type = provider.HasItemConformingTo(UtiPng) ? UtiPng
            : provider.HasItemConformingTo(UtiJpeg) ? UtiJpeg : UtiImage;
        var extension = type == UtiPng ? "png" : "jpg";
        var fileName = $"shared_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.{extension}";
        Log($"Tipo solicitado: {type}; destino: {fileName}");

        _groupContainer = NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
        Log($"GetContainerUrl: {_groupContainer?.Path ?? "null"}");
        if (_groupContainer?.Path is not { } containerPath)
        { Fail("ContaBee no pudo acceder al almacenamiento compartido."); return; }

        var destinationPath = Path.Combine(containerPath, fileName);
        var destinationUrl = NSUrl.FromFilename(destinationPath);
        try
        {
            provider.LoadDataRepresentation(type, (data, error) =>
            {
                Log($"LoadDataRepresentation: bytes={data?.Length ?? 0}; error={Describe(error)}");
                if (error != null || data == null || data.Length == 0)
                { InvokeOnMainThread(() => Fail("iOS no pudo entregar los datos de la imagen.")); return; }
                try
                {
                    var written = data.Save(destinationUrl, atomically: true);
                    var exists = File.Exists(destinationPath);
                    var length = exists ? new FileInfo(destinationPath).Length : 0;
                    Log($"Guardado: resultado={written}; existe={exists}; bytes={length}");
                    if (!written || !exists || length == 0)
                    { InvokeOnMainThread(() => Fail("No se pudo guardar la imagen compartida.")); return; }

                    var defaults = new NSUserDefaults(AppGroupId, NSUserDefaultsType.SuiteName);
                    defaults.SetString(fileName, PendingKey);
                    var synchronized = defaults.Synchronize();
                    var readBack = defaults.StringForKey(PendingKey);
                    Log($"NSUserDefaults: synchronize={synchronized}; lectura={readBack ?? "null"}");
                    if (!string.Equals(fileName, readBack, StringComparison.Ordinal))
                    { InvokeOnMainThread(() => Fail("No se pudo registrar la imagen para ContaBee.")); return; }

                    ScheduleNotification();
                    InvokeOnMainThread(() => ShowResult("Imagen guardada. Abre ContaBee para continuar.", false));
                }
                catch (Exception ex)
                { Log($"Excepción procesando datos: {ex}"); InvokeOnMainThread(() => Fail("Ocurrió un error al guardar la imagen.")); }
            });
        }
        catch (Exception ex)
        { Log($"Excepción iniciando LoadDataRepresentation: {ex}"); Fail("No se pudo leer la imagen compartida."); }
    }

    private void ScheduleNotification()
    {
        try
        {
            var content = new UNMutableNotificationContent { Title = "ContaBee",
                Body = "Tu foto está lista. Abre ContaBee para procesarla.", Sound = UNNotificationSound.Default };
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
            var request = UNNotificationRequest.FromIdentifier($"shareext_{Guid.NewGuid():N}", content, trigger);
            UNUserNotificationCenter.Current.AddNotificationRequest(request,
                error => Log($"Notificación: error={Describe(error)}"));
        }
        catch (Exception ex) { Log($"Excepción programando notificación: {ex}"); }
    }

    private void Fail(string message) => ShowResult(message, true);

    private void ShowResult(string message, bool error)
    {
        Log($"Estado de interfaz: {(error ? "error" : "éxito")} — {message}");
        _status!.Text = message;
        _status.TextColor = error ? UIColor.SystemRed : UIColor.Label;
        _done!.Hidden = false;
    }

    private void CompleteExtension()
    {
        Log("CompleteRequest");
        try { ExtensionContext?.CompleteRequest([], null); }
        catch (Exception ex) { Log($"Excepción en CompleteRequest: {ex}"); Cancel(); }
    }

    private void Cancel()
    {
        Log("CancelRequest");
        ExtensionContext?.CancelRequest(new NSError(new NSString("ContaBeeShareExtension"), 0));
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ShareExtension] {message}";
        // stdout/stderr de una extensión .NET iOS se publica en el unified log y
        // aparece en Console.app bajo el proceso ContaBeeShareExtension.
        Console.WriteLine(line);
        try
        {
            _groupContainer ??= NSFileManager.DefaultManager.GetContainerUrl(AppGroupId);
            if (_groupContainer?.Path is { } path)
                File.AppendAllText(Path.Combine(path, "shareext_log.txt"), line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ShareExtension] Error escribiendo log: {ex.Message}");
        }
    }

    private static string Describe(NSError? error) => error == null
        ? "ninguno" : $"{error.Domain}/{error.Code}: {error.LocalizedDescription}";
}
