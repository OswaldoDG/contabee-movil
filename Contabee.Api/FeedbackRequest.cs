using System.Text.Json.Serialization;

namespace Contabee.Api;

public class FeedbackRequest
{
    [JsonPropertyName("cuentaFiscalId")]
    public string CuentaFiscalId { get; set; } = string.Empty;

    [JsonPropertyName("elementos")]
    public List<FeedbackElemento> Elementos { get; set; } = new();
}

public class FeedbackElemento
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("detalle")]
    public string Detalle { get; set; } = string.Empty;
}
