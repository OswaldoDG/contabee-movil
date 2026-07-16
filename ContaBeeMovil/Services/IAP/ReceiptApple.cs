#if IOS || MACCATALYST
using ContaBeeMovil.Services.Dev;
using Foundation;
using StoreKit;

namespace ContaBeeMovil.Services.IAP;

/// <summary>
/// Lectura del recibo de App Store (base64) con refresco. En instalaciones limpias o algunos
/// escenarios de sandbox el archivo de recibo puede no existir todavía; <see cref="SKReceiptRefreshRequest"/>
/// lo materializa. Reemplaza el fallback anterior a <c>OriginalJson</c>, que no es un recibo válido.
///
/// NOTA: interop de StoreKit — no compila en Windows. Verificar en el build de iOS.
/// </summary>
internal static class ReceiptApple
{
    public static async Task<string?> LeerBase64ConRefrescoAsync(IServicioLogs logs)
    {
        var b64 = LeerBase64();
        if (b64 is not null) return b64;

        logs.Log("ReceiptApple: recibo ausente, solicitando refresh…");
        var refrescado = await RefrescarAsync();
        logs.Log($"ReceiptApple: refresh resultado={refrescado}");
        return LeerBase64();
    }

    private static string? LeerBase64()
    {
        try
        {
            var path = NSBundle.MainBundle.AppStoreReceiptUrl?.Path;
            if (path != null && System.IO.File.Exists(path))
                return Convert.ToBase64String(System.IO.File.ReadAllBytes(path));
        }
        catch
        {
            // best-effort: si no se puede leer, devolvemos null y el llamador decide.
        }
        return null;
    }

    private static Task<bool> RefrescarAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        try
        {
            var request = new SKReceiptRefreshRequest();
            request.Finished += (_, _) => tcs.TrySetResult(true);
            request.Failed += (_, _) => tcs.TrySetResult(false);
            request.Start();
        }
        catch
        {
            tcs.TrySetResult(false);
        }
        return tcs.Task;
    }
}
#endif
