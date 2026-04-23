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

    public static void NotifyAppReady()
    {
        _appReady = true;
        if (_pendingFileName != null)
            ProcessPendingImage();
    }

    /// <summary>Retorna y limpia el fileName pendiente. Llamar desde PaginaCaptura.OnAppearing.</summary>
    public static string? TakePendingSharedImage()
    {
        var fileName = _pendingFileName;
        _pendingFileName = null;
        return fileName;
    }

    private static void ProcessPendingImage()
    {
        if (_pendingFileName == null) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var tieneSesion = Preferences.Get("TieneSesion", false);
                if (!tieneSesion)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ SharedImage: usuario no autenticado, se omite navegación.");
                    return;
                }

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
