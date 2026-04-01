using Contabee.Api.Transcript;

namespace ContaBeeMovil.Models;

public class CapturaLote
{
    public TipoProcesoCaptura TipoCaptura { get; set; }
    public string Path { get; set; } = string.Empty;
}
