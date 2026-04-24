using Contabee.Api.Transcript;

namespace ContaBeeMovil.Models;

public class CapturaLote
{
    public TipoProcesoCaptura TipoCaptura { get; set; }

    /// <summary>
    /// Solo el nombre del archivo (sin directorio). Reconstruir el path completo
    /// con FileSystem.AppDataDirectory para evitar paths absolutos obsoletos.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path completo calculado en tiempo de ejecución desde FileName.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public string Path => string.IsNullOrEmpty(FileName)
        ? string.Empty
        : System.IO.Path.Combine(FileSystem.AppDataDirectory, FileName);

    /// <summary>
    /// Indica que la imagen fue recibida desde otra app (Share Extension).
    /// </summary>
    public bool EsCompartida { get; set; } = false;
}
