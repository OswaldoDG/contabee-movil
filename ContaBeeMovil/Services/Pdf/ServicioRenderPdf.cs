using SkiaSharp;

namespace ContaBeeMovil.Services.Pdf;

/// <summary>
/// Renderiza páginas de PDF a JPEG con las APIs del sistema:
/// <c>android.graphics.pdf.PdfRenderer</c> en Android y
/// <c>CoreGraphics.CGPDFDocument</c> en iOS. La rotación del usuario se aplica
/// en código compartido con SkiaSharp (mismo patrón que
/// <c>ServicioCamara.NormalizarOrientacionExif</c>). Stateless.
/// </summary>
public class ServicioRenderPdf : IServicioRenderPdf
{
    // Tope por lado del bitmap: límite seguro de textura GL en Android.
    private const int LadoMaxPx = 4096;
    private const int CalidadJpeg = 90;

    public Task<IReadOnlyList<PaginaPdfRender>> RenderizarPaginasAsync(
        string rutaPdf, int anchoPxObjetivo, int gradosRotacion = 0, CancellationToken ct = default)
    {
#if ANDROID || IOS
        int grados = ((gradosRotacion % 360) + 360) % 360;
        return Task.Run(() => Renderizar(rutaPdf, anchoPxObjetivo, grados, ct), ct);
#else
        throw new NotSupportedException("El render de PDF solo está implementado para Android e iOS.");
#endif
    }

#if ANDROID
    private static IReadOnlyList<PaginaPdfRender> Renderizar(
        string rutaPdf, int anchoPxObjetivo, int grados, CancellationToken ct)
    {
        using var archivo = new Java.IO.File(rutaPdf);
        using var descriptor = Android.OS.ParcelFileDescriptor.Open(
                archivo, Android.OS.ParcelFileMode.ReadOnly)
            ?? throw new InvalidOperationException("No se pudo abrir el PDF.");
        // PdfRenderer: una sola página abierta a la vez y no thread-safe →
        // loop secuencial dentro de este único hilo de fondo.
        using var renderer = new Android.Graphics.Pdf.PdfRenderer(descriptor);

        var paginas = new List<PaginaPdfRender>(renderer.PageCount);
        for (int i = 0; i < renderer.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var pagina = renderer.OpenPage(i)
                ?? throw new InvalidOperationException($"No se pudo leer la página {i + 1}.");

            // Width/Height ya traen aplicado el /Rotate intrínseco de la página.
            var (ancho, alto) = CalcularDimensiones(pagina.Width, pagina.Height, anchoPxObjetivo);

            using var bitmap = Android.Graphics.Bitmap.CreateBitmap(
                    ancho, alto, Android.Graphics.Bitmap.Config.Argb8888!)
                ?? throw new InvalidOperationException("No se pudo crear el bitmap.");

            // Los PDF no pintan fondo: sin esto el JPEG sale negro.
            bitmap.EraseColor(Android.Graphics.Color.White.ToArgb());

            // Matrix null = escalar la página al bitmap destino completo.
            pagina.Render(bitmap, null, null, Android.Graphics.Pdf.PdfRenderMode.ForDisplay);

            using var ms = new MemoryStream();
            bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, CalidadJpeg, ms);
            bitmap.Recycle(); // libera la memoria nativa ya, sin esperar al GC

            paginas.Add(CrearPagina(ms.ToArray(), ancho, alto, grados));
        }

        return paginas;
    }
#elif IOS
    private static IReadOnlyList<PaginaPdfRender> Renderizar(
        string rutaPdf, int anchoPxObjetivo, int grados, CancellationToken ct)
    {
        using var documento = CoreGraphics.CGPDFDocument.FromFile(rutaPdf)
            ?? throw new InvalidOperationException("No se pudo abrir el PDF.");

        var paginas = new List<PaginaPdfRender>((int)documento.Pages);
        for (int i = 1; i <= (int)documento.Pages; i++) // GetPage es 1-based
        {
            ct.ThrowIfCancellationRequested();

            using var pool = new Foundation.NSAutoreleasePool();
            using var pagina = documento.GetPage(i)
                ?? throw new InvalidOperationException($"No se pudo leer la página {i}.");

            // Dimensiones visuales: el /Rotate intrínseco intercambia los ejes.
            var caja = pagina.GetBoxRect(CoreGraphics.CGPDFBox.Crop);
            bool intercambia = pagina.RotationAngle % 180 != 0;
            double anchoPt = intercambia ? caja.Height : caja.Width;
            double altoPt  = intercambia ? caja.Width  : caja.Height;
            var (ancho, alto) = CalcularDimensiones(anchoPt, altoPt, anchoPxObjetivo);

            using var espacioColor = CoreGraphics.CGColorSpace.CreateDeviceRGB();
            using var contexto = new CoreGraphics.CGBitmapContext(
                null, ancho, alto, 8, ancho * 4, espacioColor,
                CoreGraphics.CGImageAlphaInfo.PremultipliedLast);

            // Los PDF no pintan fondo: sin esto el JPEG sale negro.
            contexto.SetFillColor(new CoreGraphics.CGColor(1f, 1f, 1f, 1f));
            contexto.FillRect(new CoreGraphics.CGRect(0, 0, ancho, alto));

            contexto.InterpolationQuality = CoreGraphics.CGInterpolationQuality.High;
            // El escalado puntos→píxeles se hace EXPLÍCITO: GetDrawingTransform hacia
            // un rect en píxeles no ampliaba la página (salía diminuta dentro del
            // lienzo). Escalamos el contexto al factor de relleno y dejamos que
            // GetDrawingTransform solo mapee la página —respetando el /Rotate
            // intrínseco— a un rect del tamaño VISUAL EN PUNTOS (PDF y
            // CGBitmapContext comparten origen abajo-izquierda: no hace falta flip).
            double escala = ancho / anchoPt;   // píxeles por punto (≈ alto/altoPt)
            contexto.ScaleCTM((nfloat)escala, (nfloat)escala);
            contexto.ConcatCTM(pagina.GetDrawingTransform(
                CoreGraphics.CGPDFBox.Crop,
                new CoreGraphics.CGRect(0, 0, anchoPt, altoPt), 0, true));
            contexto.DrawPDFPage(pagina);

            using var imagenCg = contexto.ToImage()
                ?? throw new InvalidOperationException($"No se pudo renderizar la página {i}.");
            using var imagen = new UIKit.UIImage(imagenCg);
            using var datos = imagen.AsJPEG(CalidadJpeg / 100f)
                ?? throw new InvalidOperationException($"No se pudo codificar la página {i}.");

            paginas.Add(CrearPagina(datos.ToArray(), ancho, alto, grados));
        }

        return paginas;
    }
#endif

#if ANDROID || IOS
    private static (int Ancho, int Alto) CalcularDimensiones(
        double anchoPt, double altoPt, int anchoPxObjetivo)
    {
        if (anchoPt <= 0 || altoPt <= 0) return (1, 1);

        // Ajusta al ancho objetivo, pero sin que ningún lado exceda LadoMaxPx
        // (tickets largos generan páginas muy altas).
        double escala = Math.Min(anchoPxObjetivo / anchoPt, LadoMaxPx / Math.Max(anchoPt, altoPt));
        int ancho = Math.Max(1, (int)Math.Round(anchoPt * escala));
        int alto  = Math.Max(1, (int)Math.Round(altoPt * escala));
        return (ancho, alto);
    }

    private static PaginaPdfRender CrearPagina(byte[] jpeg, int ancho, int alto, int grados)
    {
        if (grados == 0)
            return new PaginaPdfRender(jpeg, ancho, alto);

        var (rotado, anchoFinal, altoFinal) = RotarJpeg(jpeg, grados);
        return new PaginaPdfRender(rotado, anchoFinal, altoFinal);
    }

    /// <summary>Rota un JPEG en pasos de 90° (patrón de NormalizarOrientacionExif).</summary>
    private static (byte[] Jpeg, int Ancho, int Alto) RotarJpeg(byte[] jpeg, int grados)
    {
        using var original = SKBitmap.Decode(jpeg)
            ?? throw new InvalidOperationException("No se pudo decodificar la página renderizada.");

        bool intercambiarEjes = grados is 90 or 270;
        var info = new SKImageInfo(
            intercambiarEjes ? original.Height : original.Width,
            intercambiarEjes ? original.Width  : original.Height);

        using var rotado = new SKBitmap(info);
        using (var canvas = new SKCanvas(rotado))
        {
            canvas.Translate(info.Width / 2f, info.Height / 2f);
            canvas.RotateDegrees(grados);
            canvas.Translate(-original.Width / 2f, -original.Height / 2f);
            canvas.DrawBitmap(original, 0, 0);
        }

        using var imagen = SKImage.FromBitmap(rotado);
        using var data   = imagen.Encode(SKEncodedImageFormat.Jpeg, CalidadJpeg);
        return (data.ToArray(), info.Width, info.Height);
    }
#endif
}
