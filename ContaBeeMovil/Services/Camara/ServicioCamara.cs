using SkiaSharp;
using ZXing;

namespace ContaBeeMovil.Services.Camara;

public class ServicioCamara : IServicioCamara
{
    private TaskCompletionSource<string>? _scanTcs;

    // ============ MÉTODOS PARA TOMAR FOTO ============

    public async Task<string> TomarFotoAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
            return string.Empty;

        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
                return string.Empty;

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo is null)
                return string.Empty;

            var localPath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);

            await using var sourceStream = await photo.OpenReadAsync();
            await using var localStream = File.OpenWrite(localPath);
            await sourceStream.CopyToAsync(localStream);

            return localPath;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error cámara", ex.Message, "OK");
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