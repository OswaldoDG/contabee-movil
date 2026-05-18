using ContaBeeMovil.Services.Dev;
using SkiaSharp;
using ZXing;

namespace ContaBeeMovil.Services.Camara;

public class ServicioCamara : IServicioCamara
{
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioLogs _logs;
    private TaskCompletionSource<string>? _scanTcs;

    public ServicioCamara(IServicioAlerta servicioAlerta, IServicioLogs logs)
    {
        _servicioAlerta = servicioAlerta;
        _logs = logs;
    }

    // ============ MÉTODOS PARA TOMAR FOTO ============

    public async Task<string> TomarFotoAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await _servicioAlerta.MostrarAsync(
                "Permiso de cámara",
                $"La app no tiene permiso para usar la cámara (estado: {status}). Revisa los permisos del sistema e inténtalo nuevamente.",
                verBotonCancelar: false,
                confirmarText: "OK");
            return string.Empty;
        }

        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await _servicioAlerta.MostrarAsync(
                    "Cámara no disponible",
                    "Este dispositivo no permite capturar fotos desde la aplicación.",
                    verBotonCancelar: false,
                    confirmarText: "OK");
                return string.Empty;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo is null)
                return string.Empty;

            var localPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);

            await using var sourceStream = await photo.OpenReadAsync();
            await using var localStream = File.OpenWrite(localPath);
            await sourceStream.CopyToAsync(localStream);

            var existeDespues = File.Exists(localPath);
            _logs.Log($"[ServicioCamara] AppDataDirectory={FileSystem.AppDataDirectory}");
            _logs.Log($"[ServicioCamara] FileName={photo.FileName} | existe={existeDespues} | tamaño={new FileInfo(localPath).Length} bytes");

            return photo.FileName;
        }
        catch (Exception ex)
        {
            var detalleError = string.IsNullOrWhiteSpace(ex.Message)
                ? ex.GetType().Name
                : $"{ex.GetType().Name}: {ex.Message}";

            _logs.Log($"[ServicioCamara] Error al tomar foto: {ex}");
            await _servicioAlerta.MostrarAsync(
                "Error cámara",
                $"No fue posible abrir la cámara.\n\nDetalle: {detalleError}",
                verBotonCancelar: false,
                confirmarText: "OK");
            return string.Empty;
        }
    }

    public Task<string> ProcesarImagenAsync(string imagePath)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return string.Empty;

            try
            {
                using var stream = File.OpenRead(imagePath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap is null) return string.Empty;

                var width = bitmap.Width;
                var height = bitmap.Height;
                var pixels = bitmap.Pixels;
                var rgb = new byte[width * height * 3];
                for (int i = 0; i < pixels.Length; i++)
                {
                    rgb[i * 3]     = pixels[i].Red;
                    rgb[i * 3 + 1] = pixels[i].Green;
                    rgb[i * 3 + 2] = pixels[i].Blue;
                }

                var source = new RGBLuminanceSource(rgb, width, height);
                var binarizer = new ZXing.Common.HybridBinarizer(source);
                var zBitmap = new ZXing.BinaryBitmap(binarizer);
                var hints = new System.Collections.Generic.Dictionary<ZXing.DecodeHintType, object>
                {
                    { ZXing.DecodeHintType.POSSIBLE_FORMATS, new List<BarcodeFormat> { BarcodeFormat.QR_CODE } },
                    { ZXing.DecodeHintType.TRY_HARDER, true }
                };
                var reader = new ZXing.MultiFormatReader();
                reader.Hints = hints;
                var result = reader.decode(zBitmap);
                return result?.Text ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    // ============ MÉTODOS PARA QR ============

    public async Task<string> EscanearQrAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            return string.Empty;

        _scanTcs = new TaskCompletionSource<string>();

        var qrPage = MauiProgram.Services.GetService(typeof(Pages.Camara.QRPage)) as Page;
        if (qrPage == null)
            return string.Empty;

        await Shell.Current.Navigation.PushModalAsync(qrPage);

        try
        {
            var result = await _scanTcs.Task;
            return result ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            _scanTcs = null;
        }
    }

    public void SetScannedQrResult(string result)
    {
        try
        {
            _scanTcs?.TrySetResult(result);
        }
        catch { }
    }
}
