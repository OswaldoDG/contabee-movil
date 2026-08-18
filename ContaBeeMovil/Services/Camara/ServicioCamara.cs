using ContaBeeMovil.Services.Dev;
using ContaBeeMovil.Services.Permisos;
using SkiaSharp;
using ZXing;

namespace ContaBeeMovil.Services.Camara;

public class ServicioCamara : IServicioCamara
{
    private readonly IServicioAlerta _servicioAlerta;
    private readonly IServicioPermisos _permisos;
    private readonly IServicioLogs _logs;
    private TaskCompletionSource<string>? _scanTcs;

    public ServicioCamara(IServicioAlerta servicioAlerta, IServicioPermisos permisos, IServicioLogs logs)
    {
        _servicioAlerta = servicioAlerta;
        _permisos = permisos;
        _logs = logs;
    }

    // ============ MÉTODOS PARA TOMAR FOTO ============

    public async Task<string> TomarFotoAsync()
    {
        // Se pide en cada intento: si el usuario negó antes, aquí se vuelve a pedir.
        if (!await _permisos.AsegurarCamaraAsync("para tomar la foto de tu ticket"))
            return string.Empty;

        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
                return string.Empty;

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo is null)
                return string.Empty;

            var localPath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);

            byte[] bytes;
            await using (var sourceStream = await photo.OpenReadAsync())
            {
                using var ms = new MemoryStream();
                await sourceStream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            bytes = NormalizarOrientacionExif(bytes, _logs);
            File.WriteAllBytes(localPath, bytes);

            _logs.Log($"[ServicioCamara] AppDataDirectory={FileSystem.AppDataDirectory}");
            _logs.Log($"[ServicioCamara] FileName={photo.FileName} | existe={File.Exists(localPath)} | tamaño={new FileInfo(localPath).Length} bytes");

            return photo.FileName;
        }
        catch (Exception ex)
        {
            _logs.Log($"[ServicioCamara] {ex.GetType().Name}: {ex.Message}");
            await _servicioAlerta.MostrarAsync("Error cámara", "No se pudo acceder a la cámara. Intenta de nuevo.", verBotonCancelar: false, confirmarText: "OK");
            return string.Empty;
        }
    }

    private static byte[] NormalizarOrientacionExif(byte[] imageBytes, IServicioLogs? logs = null)
    {
        SKEncodedOrigin origen;
        using (var ms = new MemoryStream(imageBytes))
        using (var codec = SKCodec.Create(ms))
        {
            if (codec is null) return imageBytes;
            origen = codec.EncodedOrigin;
        }

        logs?.Info($"[ServicioCamara] EXIF galería: {origen}");

        if (origen is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
            return imageBytes;

        using var original = SKBitmap.Decode(imageBytes);
        if (original is null) return imageBytes;
        int grados = origen switch
        {
            SKEncodedOrigin.RightTop    => 90,
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftBottom  => 270,
            _ => 0
        };

        if (grados == 0) return imageBytes;

        bool intercambiarEjes = grados is 90 or 270;
        var info = new SKImageInfo(
            intercambiarEjes ? original.Height : original.Width,
            intercambiarEjes ? original.Width  : original.Height);

        using var rotado = new SKBitmap(info);
        using var canvas = new SKCanvas(rotado);
        canvas.Translate(info.Width / 2f, info.Height / 2f);
        canvas.RotateDegrees(grados);
        canvas.Translate(-original.Width / 2f, -original.Height / 2f);
        canvas.DrawBitmap(original, 0, 0);
        using var imagen = SKImage.FromBitmap(rotado);
        using var data   = imagen.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
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
        if (!await _permisos.AsegurarCamaraAsync("para escanear el código QR"))
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
