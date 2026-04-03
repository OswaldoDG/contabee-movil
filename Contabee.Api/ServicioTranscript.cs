using System.Net.Http.Headers;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using Newtonsoft.Json;
using Busqueda = Contabee.Api.Transcript.Busqueda;


namespace Contabee.Api;

public class ServicioTranscript(HttpClient httpClient) : IServicioTranscript
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ServicioTranscriptClient servicioTranscript = new(httpClient.BaseAddress!.ToString(), httpClient);
    private static readonly HttpClient _blobClient = new();

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

    public async Task<RespuestaPayload<ResumenCapturaCuentaFiscal>> GetEstadisticas(Guid cfid,int? anio,int? mes)
    {
        RespuestaPayload<ResumenCapturaCuentaFiscal> r = new();

        try
        {
            var res = await servicioTranscript.CuentafiscalAsync(cfid,anio,mes);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioIdentidad-Get estadisticas");
        }

        return r;

    }

    public async Task<RespuestaPayload<LoteCaptura>> CrearLoteAsync(
        CreaLoteCaptura request, CancellationToken ct = default)
    {
        RespuestaPayload<LoteCaptura> r = new();
        try
        {
            r.Payload = await servicioTranscript.LotePOSTAsync(request, ct);
            r.Ok = true;
        }
        catch (ApiException ex) when (ex.StatusCode == 201)
        {
            r.Payload = JsonConvert.DeserializeObject<LoteCaptura>(ex.Response);
            r.Ok      = r.Payload is not null;
            if (!r.Ok)
                r.Error = new ErrorProceso { Mensaje = "Respuesta vacía al crear lote.", Origen = "ServicioTranscript-CrearLote" };
        }
        catch (ApiException ex) when (ex.StatusCode == 402)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = System.Net.HttpStatusCode.PaymentRequired,
                Origen   = "ServicioTranscript-CrearLote"
            };
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearLote"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-CrearLote");
        }
        return r;
    }

    public async Task<RespuestaPayload<DtoLoteCapturaCreado>> ObtenerPrecargaAsync(
        long loteId, CancellationToken ct = default)
    {
        RespuestaPayload<DtoLoteCapturaCreado> r = new();
        try
        {
            r.Payload = await servicioTranscript.PrecargaAsync(loteId, ct);
            r.Ok = true;
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ObtenerPrecarga");
        }
        return r;
    }

    public async Task<Respuesta> SubirArchivosBlobAsync(
        string sasToken,
        IReadOnlyList<string> rutasArchivos,
        IProgress<double>? progreso = null,
        CancellationToken ct = default)
    {
        if (!sasToken.Contains("/ARCHIVO", StringComparison.Ordinal))
            return new Respuesta
            {
                Error = new ErrorProceso
                {
                    Mensaje = "El SAS token no tiene el formato esperado (/ARCHIVO).",
                    Origen  = "ServicioTranscript-SubirArchivosBlobAsync"
                }
            };

        var total = rutasArchivos.Count;
        for (int i = 0; i < total; i++)
        {
            var ruta      = rutasArchivos[i];
            var indice    = (i + 1).ToString("D3");
            var extension = Path.GetExtension(ruta).ToLowerInvariant();
            var fileName  = $"{indice}{extension}";
            var url       = sasToken.Replace("/ARCHIVO", $"/{fileName}", StringComparison.Ordinal);

            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".pdf"            => "application/pdf",
                _                 => "application/octet-stream"
            };

            if (!File.Exists(ruta))
                return new Respuesta
                {
                    Error = new ErrorProceso
                    {
                        Mensaje = $"El archivo {indice} ya no está disponible en el dispositivo.",
                        Origen  = "ServicioTranscript-SubirArchivosBlobAsync"
                    }
                };

            var bytes = await File.ReadAllBytesAsync(ruta, ct);

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Headers.Add("x-ms-blob-type", "BlockBlob");

            var response = await _blobClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new Respuesta
                {
                    Error = new ErrorProceso
                    {
                        Mensaje  = $"Error HTTP {(int)response.StatusCode} al subir {fileName}",
                        HttpCode = response.StatusCode,
                        Origen   = "ServicioTranscript-SubirArchivosBlobAsync"
                    }
                };
            }

            progreso?.Report((double)(i + 1) / total);
        }

        return new Respuesta { Ok = true };
    }

    public async Task<Respuesta> CompletarLoteAsync(long loteId, CancellationToken ct = default)
    {
        Respuesta r = new();
        try
        {
            await servicioTranscript.Completar2Async(loteId, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CompletarLote"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-CompletarLote");
        }
        return r;
    }
}

