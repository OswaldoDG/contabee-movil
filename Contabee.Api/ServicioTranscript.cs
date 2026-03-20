using System.Text.Json;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;


namespace Contabee.Api;

public class ServicioTranscript(HttpClient httpClient) : IServicioTranscript
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ServicioTranscriptClient servicioTranscript = new(httpClient.BaseAddress!.ToString(), httpClient);

    public async Task<ResultadoPaginado_1OfOfElementoPaginaCapturaDespliegueAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaCapturas(Busqueda consulta)
    {
        BusquedaCaptura consultaMap =  Extensiones.MapearA<BusquedaCaptura>(consulta);
        var result = await servicioTranscript.TrabajosAsync(consultaMap);

        return result;
    }

    public async Task<(byte[] Contenido, string TipoContenido)?> DescargarArchivoAsync(
        long id, string? tipo, CancellationToken ct = default)
    {
        var url = $"captura/pagina/contenido/{id}";
        if (!string.IsNullOrEmpty(tipo) && tipo != "imagen")
            url += $"?tipo={Uri.EscapeDataString(tipo)}";

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType
                          ?? "application/octet-stream";
        return (bytes, contentType);
    }
}

