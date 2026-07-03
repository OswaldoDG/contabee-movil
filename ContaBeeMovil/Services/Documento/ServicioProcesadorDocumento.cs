using System.Diagnostics;
using ContaBeeMovil.Services.Dev;
using Plugin.Maui.OCR;
using SkiaSharp;

namespace ContaBeeMovil.Services.Documento;

public class ServicioProcesadorDocumento : IServicioProcesadorDocumento
{
    private readonly IOcrService _ocr;
    private readonly IServicioLogs _logs;
    private bool _ocrInicializado;

    public ServicioProcesadorDocumento(IOcrService ocr, IServicioLogs logs)
    {
        _ocr = ocr;
        _logs = logs;
    }

    public async Task<string> ProcesarYGenerarPdfAsync(string rutaImagenOriginal,
        IProgress<double>? progreso = null)
    {
        progreso?.Report(0.0);
        byte[] imagenCompleta = File.ReadAllBytes(rutaImagenOriginal);
        progreso?.Report(0.2);

        int anchoImagen = 600, altoImagen = 900;
        using (var bmp = SKBitmap.Decode(imagenCompleta))
        {
            if (bmp != null) { anchoImagen = bmp.Width; altoImagen = bmp.Height; }
        }

        IEnumerable<OcrResult.OcrElement> elementos = [];
        IEnumerable<string> lineas = [];
        long msOcr = 0;

        var swOcr = Stopwatch.StartNew();
        try
        {
            var imagenCaptura = imagenCompleta;
            var tareaOcr = Task.Run(async () =>
            {
                await EnsureOcrInicializadoAsync();
                return await _ocr.RecognizeTextAsync(imagenCaptura, tryHard: false);
            });

            if (await Task.WhenAny(tareaOcr, Task.Delay(15_000)) == tareaOcr
                && tareaOcr.IsCompletedSuccessfully)
            {
                var resultado = await tareaOcr;
                if (resultado.Success)
                {
                    elementos = resultado.Elements ?? [];
                    lineas = resultado.Lines ?? [];
                    _logs.Info($"[PDF] OCR OK: {elementos.Count()} elementos, {anchoImagen}×{altoImagen}px");
                }
            }
            else
            {
                _logs.Warn("[PDF] OCR excedió 15 s, PDF sin texto");
            }
            progreso?.Report(0.75);
        }
        catch (Exception ex)
        {
            _logs.Warn($"[PDF] OCR falló: {ex.Message}");
            progreso?.Report(0.75);
        }
        finally
        {
            swOcr.Stop();
            msOcr = swOcr.ElapsedMilliseconds;
        }

        byte[] imagenReducida = ReducirImagen(imagenCompleta, _logs);
        var rutaImagenReducida = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            System.IO.Path.GetFileNameWithoutExtension(rutaImagenOriginal) + "_send.jpg");
        File.WriteAllBytes(rutaImagenReducida, imagenReducida);
        progreso?.Report(0.85);

        var rutaPdf = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            System.IO.Path.GetFileNameWithoutExtension(rutaImagenOriginal) + ".pdf");

        long msPdf = 0;
        var swPdf = Stopwatch.StartNew();
        try
        {
            GenerarPdfConImagen(rutaImagenReducida, elementos, lineas, anchoImagen, altoImagen, rutaPdf, _logs);
        }
        finally
        {
            swPdf.Stop();
            msPdf = swPdf.ElapsedMilliseconds;
            try { File.Delete(rutaImagenReducida); } catch { }
        }
        progreso?.Report(1.0);

        _logs.Info($"[PDF] timing: OCR={msOcr}ms PDF={msPdf}ms");

        long tamanoFinal = new FileInfo(rutaPdf).Length;
        long tamanoOriginal = new FileInfo(rutaImagenOriginal).Length;
        _logs.Info($"[PDF] original={tamanoOriginal / 1024}KB → pdf={tamanoFinal / 1024}KB");

        return rutaPdf;
    }

    private static void GenerarPdfConImagen(
        string rutaImagen,
        IEnumerable<OcrResult.OcrElement> elementos,
        IEnumerable<string> lineas,
        int anchoImagen, int altoImagen,
        string rutaSalida,
        IServicioLogs? logs = null)
    {
        if (anchoImagen < 1) anchoImagen = 1;
        if (altoImagen < 1) altoImagen = 1;
        const float anchoPaginaPt = 595f;
        float escala = anchoPaginaPt / anchoImagen;
        float pageWidth = anchoPaginaPt;
        float pageHeight = altoImagen * escala;

        using var outputStream = new FileStream(rutaSalida, FileMode.Create, FileAccess.Write);
        using var doc = SKDocument.CreatePdf(outputStream);
        var canvas = doc.BeginPage(pageWidth, pageHeight);

        using (var imageData = SKData.Create(rutaImagen))
        using (var imagen = SKImage.FromEncodedData(imageData))
        {
            if (imagen != null)
                canvas.DrawImage(imagen, SKRect.Create(0, 0, pageWidth, pageHeight));
        }

        var listaElementos = elementos.Where(e => !string.IsNullOrWhiteSpace(e.Text)).ToList();
        using var textPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 1),
            IsAntialias = false,
        };

        if (listaElementos.Count > 0)
        {
            var muestra = listaElementos.FirstOrDefault(e => e.Width > 0);
            bool coordsNorm = muestra != null && muestra.Width <= 1.5 && muestra.Height <= 1.5;
            logs?.Info($"[PDF] coords {(coordsNorm ? "normalizadas" : "píxeles")}, escala={escala:F4}, muestra x={muestra?.X:F3} y={muestra?.Y:F3} w={muestra?.Width:F3}");

            foreach (var elem in listaElementos)
            {
                double pxImg = coordsNorm ? elem.X * anchoImagen : elem.X;
                double pyImg = coordsNorm ? elem.Y * altoImagen : elem.Y;
                double pwImg = coordsNorm ? elem.Width * anchoImagen : elem.Width;
                double phImg = coordsNorm ? elem.Height * altoImagen : elem.Height;

                if (pwImg < 1 || phImg < 1) continue;
                float xPt = (float)(pxImg * escala);
                float yPt = (float)(pyImg * escala);
                float wPt = (float)(pwImg * escala);
                float hPt = (float)(phImg * escala);
                float textSize = Math.Max(2f, hPt * 0.8f);
                textPaint.TextSize = textSize;

                float anchoMedido = textPaint.MeasureText(elem.Text);
                if (anchoMedido > 0 && wPt > 0)
                {
                    float ajuste = wPt / anchoMedido;
                    ajuste = Math.Clamp(ajuste, 0.3f, 3.0f);
                    textPaint.TextSize = Math.Max(2f, textSize * ajuste);
                }
                canvas.DrawText(elem.Text, xPt, yPt + hPt, textPaint);
            }
        }
        else
        {
            var listaLineas = lineas.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (listaLineas.Count > 0)
            {
                float lineHeight = pageHeight / listaLineas.Count;
                textPaint.TextSize = Math.Max(2f, lineHeight * 0.6f);
                for (int i = 0; i < listaLineas.Count; i++)
                    canvas.DrawText(listaLineas[i], 4f, (i + 1) * lineHeight, textPaint);
                logs?.Info($"[PDF] fallback Lines: {listaLineas.Count}");
            }
            else
            {
                logs?.Warn("[PDF] OCR sin texto — solo imagen");
            }
        }

        doc.EndPage();
        doc.Close();
        logs?.Info($"[PDF] Guardado: {rutaSalida} ({pageWidth:F0}×{pageHeight:F0}pt)");
    }

    // ── OCR init ─────────────────────────────────────────────────────────────

    private async Task EnsureOcrInicializadoAsync()
    {
        if (_ocrInicializado) return;
        await _ocr.InitAsync();
        _ocrInicializado = true;
    }

    // ── Reducción de tamaño ──────────────────────────────────────────────────
    // Ancho y alto se reducen al 40% (no 25%): a 25% por eje la imagen quedaba
    // con muy pocos píxeles reales y, al estirarse dentro de la página del PDF
    // (que se dimensiona con el tamaño ORIGINAL para que las coordenadas del
    // OCR calcen), se veía notoriamente pixelada. 40% es un punto medio entre
    // peso de archivo y nitidez del texto del ticket.
    private const double FactorReduccion = 0.4;

    private static byte[] ReducirImagen(byte[] imagenJpeg, IServicioLogs? logs = null)
    {
        using var original = SKBitmap.Decode(imagenJpeg);
        if (original == null) return imagenJpeg;

        int anchoFinal = (int)(original.Width * FactorReduccion);
        int alturaFinal = (int)(original.Height * FactorReduccion);
        if (anchoFinal < 1 || alturaFinal < 1) return imagenJpeg;

        using var reducido = original.Resize(
            new SKImageInfo(anchoFinal, alturaFinal),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var imagen = SKImage.FromBitmap(reducido);
        using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 80);
        var resultado = datos.ToArray();
        logs?.Info($"[PDF] Reducción {FactorReduccion:P0}: {original.Width}×{original.Height} → {anchoFinal}×{alturaFinal}");
        return resultado;
    }
}
