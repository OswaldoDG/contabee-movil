namespace ContaBeeMovil.Services.Documento;

public interface IServicioProcesadorDocumento
{
    Task<string> ProcesarYGenerarPdfAsync(string rutaImagenOriginal,
        IProgress<double>? progreso = null);
}
