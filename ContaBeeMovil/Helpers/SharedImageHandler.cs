using Contabee.Api.Transcript;

namespace ContaBeeMovil.Helpers;

public static class SharedImageHandler
{
    private static string? _pendingFileName;
    private static bool _appReady;

    public static void HandleSharedImage(string fileName)
    {
        System.Diagnostics.Debug.WriteLine($"📷 SharedImage: imagen recibida = {fileName}");
        _pendingFileName = fileName;
        if (_appReady)
            ProcessPendingImage();
    }

    // iOS: llamar desde AppDelegate.OpenUrl o FinishedLaunching cuando llega contabee://shared-image
    // No accede al App Group aquí — solo señala y delega a ProcessIosScheme cuando la app esté lista
    public static void NotifySharedImageScheme()
    {
        System.Diagnostics.Debug.WriteLine("📷 SharedImage: URL scheme recibida");
        if (_appReady)
            TriggerIosSchemeProcessing();
        // Si la app no está lista, NotifyAppReady llamará TriggerIosSchemeProcessing de todas formas
    }

    public static void NotifyAppReady()
    {
        System.Diagnostics.Debug.WriteLine("✅ SharedImage: app lista");
        _appReady = true;
        ReadExtensionDiagLog();
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

    // Lee y vuelca el log de diagnóstico que escribe ShareViewController en el App Group.
    // Aparece en Console vía Debug.WriteLine del proceso principal (que sí va a NSLog en MAUI).
    private static void ReadExtensionDiagLog()
    {
#if IOS || MACCATALYST
        try
        {
            const string appGroupId = "group.mx.contabee.app";
            var container = Foundation.NSFileManager.DefaultManager.GetContainerUrl(appGroupId);
            if (container?.Path is not string p) return;
            var logPath = System.IO.Path.Combine(p, "shareext_log.txt");
            if (!System.IO.File.Exists(logPath)) return;
            var content = System.IO.File.ReadAllText(logPath);
            System.IO.File.Delete(logPath);
            System.Diagnostics.Debug.WriteLine($"📋 ShareExt diag log:\n{content}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ SharedImage: error leyendo diag log — {ex.Message}");
        }
#endif
    }

    public static string? TakePendingSharedImage()
    {
        var fileName = _pendingFileName;
        _pendingFileName = null;
        return fileName;
    }

    private static void TriggerIosSchemeProcessing()
    {
        // Diferir al hilo principal después de que OpenUrl termine y MAUI estabilice
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📷 SharedImage: procesando URL scheme en hilo principal");
                await Task.Run(ReadAndCopyFromAppGroupAsync);
                if (_pendingFileName != null)
                {
                    System.Diagnostics.Debug.WriteLine($"📷 SharedImage: archivo listo: {_pendingFileName}");
                    ProcessPendingImage();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ SharedImage: no se encontró archivo en App Group");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SharedImage: error procesando URL scheme — {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"📷 SharedImage: NSUserDefaults fileName = '{fileName}'");

            if (string.IsNullOrEmpty(fileName)) return;

            var groupContainer = Foundation.NSFileManager.DefaultManager.GetContainerUrl(appGroupId);
            System.Diagnostics.Debug.WriteLine($"📷 SharedImage: groupContainer = {groupContainer?.Path}");
            if (groupContainer?.Path is not { } containerPath) return;

            var srcPath = System.IO.Path.Combine(containerPath, fileName);
            var destPath = System.IO.Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, fileName);

            System.Diagnostics.Debug.WriteLine($"📷 SharedImage: src={srcPath} existe={System.IO.File.Exists(srcPath)}");

            if (!System.IO.File.Exists(srcPath)) return;

            System.IO.File.Copy(srcPath, destPath, overwrite: true);
            defaults.RemoveObject("pendingSharedImage");
            System.Diagnostics.Debug.WriteLine($"📷 SharedImage: copiado a {destPath}");
            _pendingFileName = fileName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ SharedImage: error copiando archivo — {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("⚠️ SharedImage: no autenticado, se omite navegación");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("📷 SharedImage: navegando a PaginaCaptura");
                await Shell.Current.GoToAsync("PaginaCaptura",
                    new Dictionary<string, object> { ["tipo"] = TipoProcesoCaptura.FacturaIndividual });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SharedImage: error navegando — {ex.Message}");
            }
        });
    }
}
