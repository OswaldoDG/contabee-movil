namespace ContaBeeMovil.Services.Pdf;

/// <summary>Página de un PDF renderizada a JPEG en memoria.</summary>
public sealed record PaginaPdfRender(byte[] Jpeg, int AnchoPx, int AltoPx)
{
    /// <summary>Relación ancho/alto de la imagen final (ya rotada).</summary>
    public double Aspecto => AltoPx == 0 ? 1 : (double)AnchoPx / AltoPx;
}

/// <summary>
/// Renderiza las páginas de un PDF a imágenes usando las APIs del sistema
/// (PdfRenderer en Android, CoreGraphics en iOS), sin dependencias externas.
/// </summary>
public interface IServicioRenderPdf
{
    /// <summary>
    /// Renderiza todas las páginas del PDF a JPEG en memoria.
    /// <paramref name="anchoPxObjetivo"/> es el ancho deseado en píxeles de la
    /// página "derecha" (antes de la rotación del usuario); internamente se
    /// acota para que ningún lado exceda el máximo seguro de bitmap.
    /// <paramref name="gradosRotacion"/> (0/90/180/270) es la rotación del
    /// usuario, aplicada sobre el bitmap ya renderizado.
    /// Corre en hilo de fondo; lanza excepción si el PDF no puede abrirse y
    /// OperationCanceledException si se cancela entre páginas.
    /// </summary>
    Task<IReadOnlyList<PaginaPdfRender>> RenderizarPaginasAsync(
        string rutaPdf,
        int anchoPxObjetivo,
        int gradosRotacion = 0,
        CancellationToken ct = default);
}
