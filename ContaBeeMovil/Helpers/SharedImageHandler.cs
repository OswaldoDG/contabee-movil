using Contabee.Api.Transcript;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Helpers;

public static class SharedImageHandler
{
    public static event Action<string>? ImagenCompartidaRecibida;

    private static string? _pendingFileName;
    private static bool _appReady;
    private static bool _processingAppGroup;

    private static IServicioLogs? Logs => App.Services?.GetService<IServicioLogs>();

    public static void HandleSharedImage(string fileName)
    {
        Logs?.Log($"[SharedImage] imagen recibida (Android) — {fileName}");
        _pendingFileName = fileName;
        if (_appReady)
            ProcessPendingImage();
    }

    // iOS: llamar desde AppDelegate.OpenUrl o FinishedLaunching cuando llega contabee://shared-image
    // No accede al App Group aquí — solo señala y delega a ProcessIosScheme cuando la app esté lista
    public static void NotifySharedImageScheme()
    {
        Logs?.Log("[SharedImage] URL scheme recibida (iOS)");
        if (_appReady)
            TriggerIosSchemeProcessing();
        // Si la app no está lista, NotifyAppReady llamará TriggerIosSchemeProcessing de todas formas
    }

    public static void NotifyAppReady()
    {
        Logs?.Log("[SharedImage] app lista");
        _appReady = true;
        if (_pendingFileName != null)
        {
            // Android ya entregó el archivo directamente
            ProcessPendingImage();
        }
        else
        {
            // iOS: revisar App Group siempre — cubre tanto apertura por URL scheme
            // como apertura manual cuando openURL falló desde la extensión
            TriggerIosSchemeProcessing();
        }
    }

    public static string? TakePendingSharedImage()
    {
        var fileName = _pendingFileName;
        _pendingFileName = null;
        return fileName;
    }

    private static void TriggerIosSchemeProcessing()
    {
        // Evitar doble navegación cuando AppDelegate.OpenUrl y OnResume se disparan juntos
        if (_processingAppGroup) return;
        _processingAppGroup = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Logs?.Log("[SharedImage] leyendo App Group...");
                await Task.Run(ReadAndCopyFromAppGroupAsync);
                if (_pendingFileName != null)
                {
                    Logs?.Log($"[SharedImage] archivo listo — {_pendingFileName}");
                    ProcessPendingImage();
                }
                else
                {
                    Logs?.Log("[SharedImage] no se encontró archivo en App Group");
                }
            }
            catch (Exception ex)
            {
                Logs?.Log($"[SharedImage] error procesando App Group — {ex.Message}");
            }
            finally
            {
                _processingAppGroup = false;
            }
        });
    }

    // Ejecuta en un hilo de fondo — no usa APIs de MAUI
    private static void ReadAndCopyFromAppGroupAsync()
    {
#if IOS || MACCATALYST
        try
        {
            const string appGroupId = "group.mx.contabee.app";
            var defaults = new Foundation.NSUserDefaults(appGroupId, Foundation.NSUserDefaultsType.SuiteName);
            var fileName = defaults.StringForKey("pendingSharedImage");

            if (string.IsNullOrEmpty(fileName)) return;

            var groupContainer = Foundation.NSFileManager.DefaultManager.GetContainerUrl(appGroupId);
            if (groupContainer?.Path is not { } containerPath) return;

            var srcPath = System.IO.Path.Combine(containerPath, fileName);
            var destPath = System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, fileName);

            if (!System.IO.File.Exists(srcPath)) return;

            System.IO.File.Copy(srcPath, destPath, overwrite: true);
            defaults.RemoveObject("pendingSharedImage");
            Logs?.Log($"[SharedImage] archivo copiado desde App Group — {fileName}");
            _pendingFileName = fileName;
        }
        catch (Exception ex)
        {
            Logs?.Log($"[SharedImage] error copiando desde App Group — {ex.Message}");
        }
#endif
    }

    private static void ProcessPendingImage()
    {
        if (_pendingFileName == null) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // Pequeña pausa para asegurar que la transición de MAUI terminó
                await Task.Delay(300);

                var tieneSesion = Preferences.Get("TieneSesion", false);
                if (!tieneSesion)
                {
                    Logs?.Log("[SharedImage] sin sesión activa — se omite navegación");
                    return;
                }

                // Si PaginaCaptura ya está visible y suscrita, entregar la imagen sin navegar
                if (ImagenCompartidaRecibida != null)
                {
                    var fileName = TakePendingSharedImage();
                    Logs?.Log($"[SharedImage] entregando a PaginaCaptura activa — {fileName}");
                    ImagenCompartidaRecibida(fileName!);
                    return;
                }

                Logs?.Log("[SharedImage] navegando a PaginaCaptura");
                await Shell.Current.GoToAsync("PaginaCaptura",
                    new Dictionary<string, object> { ["tipo"] = TipoProcesoCaptura.FacturaIndividual });
            }
            catch (Exception ex)
            {
                Logs?.Log($"[SharedImage] error navegando a PaginaCaptura — {ex.Message}");
            }
        });
    }
}
