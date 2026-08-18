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

    public async Task<ResultadoPaginado_1OfOfComprobacionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaComprobaciones(Busqueda consulta)
    {
        Busqueda consultaMap =  Extensiones.MapearA<Busqueda>(consulta);
        var result = await servicioTranscript.BuscarAsync(consultaMap);
        return result;
    }

    public async Task<ResultadoPaginado_1OfOfDevolucionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaDevoluciones(Busqueda consulta)
    {
        Busqueda consultaMap =  Extensiones.MapearA<Busqueda>(consulta);
        var result = await servicioTranscript.Buscar2Async(consultaMap);
        return result;
    }

    public async Task<RespuestaPayload<Devolucion>> CrearDevolucionAsync(
        CreaDevolucion request, CancellationToken ct = default)
    {
        RespuestaPayload<Devolucion> r = new();
        try
        {
            r.Payload = await servicioTranscript.DevolucionPOSTAsync(request, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearDevolucion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-CrearDevolucion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Comprobacion>> ObtenerComprobacionAsync(
        Guid id, CancellationToken ct = default)
    {
        RespuestaPayload<Comprobacion> r = new();
        try
        {
            r.Payload = await servicioTranscript.ComprobacionGETAsync(id, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerComprobacion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ObtenerComprobacion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Devolucion>> ObtenerDevolucionAsync(
        Guid id, CancellationToken ct = default)
    {
        RespuestaPayload<Devolucion> r = new();
        try
        {
            r.Payload = await servicioTranscript.DevolucionGETAsync(id, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerDevolucion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ObtenerDevolucion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Comprobacion>> ActualizarComprobacionAsync(
        Guid id, ActualizaComprobacion request, CancellationToken ct = default)
    {
        RespuestaPayload<Comprobacion> r = new();
        try
        {
            r.Payload = await servicioTranscript.ComprobacionPUTAsync(id, request, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarComprobacion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ActualizarComprobacion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Devolucion>> ActualizarDevolucionAsync(
        Guid id, ActualizaDevolucion request, CancellationToken ct = default)
    {
        RespuestaPayload<Devolucion> r = new();
        try
        {
            r.Payload = await servicioTranscript.DevolucionPUTAsync(id, request, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarDevolucion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ActualizarDevolucion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Comprobacion>> ActualizarEstadoComprobacionAsync(
        Guid id, EstadoComprobacion estado, CancellationToken ct = default)
    {
        RespuestaPayload<Comprobacion> r = new();
        try
        {
            r.Payload = await servicioTranscript.EstadoPUTAsync(id, estado, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarEstadoComprobacion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ActualizarEstadoComprobacion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Devolucion>> ActualizarEstadoDevolucionAsync(
        Guid id, EstadoDevolucion estado, CancellationToken ct = default)
    {
        RespuestaPayload<Devolucion> r = new();
        try
        {
            r.Payload = await servicioTranscript.EstadoPUT2Async(id, estado, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarEstadoDevolucion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ActualizarEstadoDevolucion");
        }
        return r;
    }

    public async Task<Respuesta> EliminarComprobacionAsync(
        Guid id, CancellationToken ct = default)
    {
        Respuesta r = new();
        try
        {
            await servicioTranscript.ComprobacionDELETEAsync(id, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-EliminarComprobacion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-EliminarComprobacion");
        }
        return r;
    }

    public async Task<Respuesta> EliminarDevolucionAsync(
        Guid id, CancellationToken ct = default)
    {
        Respuesta r = new();
        try
        {
            await servicioTranscript.DevolucionDELETEAsync(id, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-EliminarDevolucion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-EliminarDevolucion");
        }
        return r;
    }

    public async Task<RespuestaPayload<Comprobacion>> CrearComprobacionAsync(
        CreaComprobacion request, CancellationToken ct = default)
    {
        RespuestaPayload<Comprobacion> r = new();
        try
        {
            r.Payload = await servicioTranscript.ComprobacionPOSTAsync(request, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearComprobacion"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-CrearComprobacion");
        }
        return r;
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
            var res = await servicioTranscript.CuentafiscalAsync(cfid,null,anio,mes);
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

    public async Task<RespuestaPayload<ValorInstantaneoCaptura>> ObtenerInstantaneosAsync(
        CancellationToken ct = default)
    {
        RespuestaPayload<ValorInstantaneoCaptura> r = new();
        try
        {
            r.Payload = await servicioTranscript.InstantaneosAsync(ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerInstantaneos"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ObtenerInstantaneos");
        }

        return r;
    }

    public async Task<RespuestaPayload<ICollection<DiaInhabil>>> ObtenerDiasInhabilesAsync(
        string pais, int ano, CancellationToken ct = default)
    {
        RespuestaPayload<ICollection<DiaInhabil>> r = new();
        try
        {
            r.Payload = await servicioTranscript.DiasinhabilesAsync(pais, ano, ct);
            r.Ok = true;
        }
        catch (ApiException ex)
        {
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerDiasInhabiles"
            };
        }
        catch (Exception ex)
        {
            r.Error = ex.ErrorGenerico("ServicioTranscript-ObtenerDiasInhabiles");
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

    public async Task<Respuesta> CompletarLoteAsync(long loteId, DtoCierreLote? lote = null, CancellationToken ct = default)
    {
        Respuesta r = new();
        try
        {
            await servicioTranscript.Completar2Async(loteId, lote ?? new DtoCierreLote());
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

