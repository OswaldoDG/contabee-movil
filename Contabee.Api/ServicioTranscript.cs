using System.Net.Http.Headers;
using Contabee.Api.abstractions;
using Contabee.Api.Transcript;
using Contabee.Api.Logging;
using Newtonsoft.Json;
using Busqueda = Contabee.Api.Transcript.Busqueda;


namespace Contabee.Api;

public class ServicioTranscript(HttpClient httpClient, IAppLogger logger) : IServicioTranscript
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ServicioTranscriptClient servicioTranscript = new(httpClient.BaseAddress!.ToString(), httpClient);
    private static readonly HttpClient _blobClient = new();
    private readonly IAppLogger _logger = logger;

    public async Task<ResultadoPaginado_1OfOfElementoPaginaCapturaDespliegueAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaCapturas(Busqueda consulta)
    {
        _logger.Info("ServicioTranscript.BusquedaCapturas", "Inicio de búsqueda de capturas.");
        try
        {
            BusquedaCaptura consultaMap =  Extensiones.MapearA<BusquedaCaptura>(consulta);
            var result = await servicioTranscript.TrabajosAsync(consultaMap);
            _logger.Info("ServicioTranscript.BusquedaCapturasExitoso", "Búsqueda de capturas completada.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.BusquedaCapturasException", "Excepción no controlada al buscar capturas.", ex);
            throw;
        }
    }

    public async Task<ResultadoPaginado_1OfOfComprobacionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaComprobaciones(Busqueda consulta)
    {
        _logger.Info("ServicioTranscript.BusquedaComprobaciones", "Inicio de búsqueda de comprobaciones.");
        try
        {
            Busqueda consultaMap =  Extensiones.MapearA<Busqueda>(consulta);
            var result = await servicioTranscript.BuscarAsync(consultaMap);
            _logger.Info("ServicioTranscript.BusquedaComprobacionesExitoso", "Búsqueda de comprobaciones completada.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.BusquedaComprobacionesException", "Excepción no controlada al buscar comprobaciones.", ex);
            throw;
        }
    }

    public async Task<ResultadoPaginado_1OfOfDevolucionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaDevoluciones(Busqueda consulta)
    {
        _logger.Info("ServicioTranscript.BusquedaDevoluciones", "Inicio de búsqueda de devoluciones.");
        try
        {
            Busqueda consultaMap =  Extensiones.MapearA<Busqueda>(consulta);
            var result = await servicioTranscript.Buscar2Async(consultaMap);
            _logger.Info("ServicioTranscript.BusquedaDevolucionesExitoso", "Búsqueda de devoluciones completada.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.BusquedaDevolucionesException", "Excepción no controlada al buscar devoluciones.", ex);
            throw;
        }
    }

    public async Task<RespuestaPayload<Devolucion>> CrearDevolucionAsync(
        CreaDevolucion request, CancellationToken ct = default)
    {
        RespuestaPayload<Devolucion> r = new();
        try
        {
            _logger.Info("ServicioTranscript.CrearDevolucion", "Inicio de creación de devolución.");
            r.Payload = await servicioTranscript.DevolucionPOSTAsync(request, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.CrearDevolucionExitoso", "Creación de devolución completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.CrearDevolucionApiException", "Error API al crear devolución.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearDevolucion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.CrearDevolucionException", "Excepción no controlada al crear devolución.", ex);
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
            _logger.Info("ServicioTranscript.ObtenerComprobacion", "Inicio de consulta de comprobación.");
            r.Payload = await servicioTranscript.ComprobacionGETAsync(id, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ObtenerComprobacionExitoso", "Consulta de comprobación completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ObtenerComprobacionApiException", "Error API al obtener comprobación.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerComprobacion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ObtenerComprobacionException", "Excepción no controlada al obtener comprobación.", ex);
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
            _logger.Info("ServicioTranscript.ObtenerDevolucion", "Inicio de consulta de devolución.");
            r.Payload = await servicioTranscript.DevolucionGETAsync(id, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ObtenerDevolucionExitoso", "Consulta de devolución completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ObtenerDevolucionApiException", "Error API al obtener devolución.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ObtenerDevolucion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ObtenerDevolucionException", "Excepción no controlada al obtener devolución.", ex);
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
            _logger.Info("ServicioTranscript.ActualizarComprobacion", "Inicio de actualización de comprobación.");
            r.Payload = await servicioTranscript.ComprobacionPUTAsync(id, request, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ActualizarComprobacionExitoso", "Actualización de comprobación completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarComprobacionApiException", "Error API al actualizar comprobación.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarComprobacion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarComprobacionException", "Excepción no controlada al actualizar comprobación.", ex);
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
            _logger.Info("ServicioTranscript.ActualizarDevolucion", "Inicio de actualización de devolución.");
            r.Payload = await servicioTranscript.DevolucionPUTAsync(id, request, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ActualizarDevolucionExitoso", "Actualización de devolución completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarDevolucionApiException", "Error API al actualizar devolución.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarDevolucion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarDevolucionException", "Excepción no controlada al actualizar devolución.", ex);
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
            _logger.Info("ServicioTranscript.ActualizarEstadoComprobacion", "Inicio de actualización de estado de comprobación.");
            r.Payload = await servicioTranscript.EstadoPUTAsync(id, estado, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ActualizarEstadoComprobacionExitoso", "Actualización de estado de comprobación completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarEstadoComprobacionApiException", "Error API al actualizar estado de comprobación.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarEstadoComprobacion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarEstadoComprobacionException", "Excepción no controlada al actualizar estado de comprobación.", ex);
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
            _logger.Info("ServicioTranscript.ActualizarEstadoDevolucion", "Inicio de actualización de estado de devolución.");
            r.Payload = await servicioTranscript.EstadoPUT2Async(id, estado, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ActualizarEstadoDevolucionExitoso", "Actualización de estado de devolución completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarEstadoDevolucionApiException", "Error API al actualizar estado de devolución.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-ActualizarEstadoDevolucion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ActualizarEstadoDevolucionException", "Excepción no controlada al actualizar estado de devolución.", ex);
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
            _logger.Info("ServicioTranscript.EliminarComprobacion", "Inicio de eliminación de comprobación.");
            await servicioTranscript.ComprobacionDELETEAsync(id, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.EliminarComprobacionExitoso", "Eliminación de comprobación completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.EliminarComprobacionApiException", "Error API al eliminar comprobación.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-EliminarComprobacion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.EliminarComprobacionException", "Excepción no controlada al eliminar comprobación.", ex);
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
            _logger.Info("ServicioTranscript.EliminarDevolucion", "Inicio de eliminación de devolución.");
            await servicioTranscript.DevolucionDELETEAsync(id, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.EliminarDevolucionExitoso", "Eliminación de devolución completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.EliminarDevolucionApiException", "Error API al eliminar devolución.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-EliminarDevolucion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.EliminarDevolucionException", "Excepción no controlada al eliminar devolución.", ex);
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
            _logger.Info("ServicioTranscript.CrearComprobacion", "Inicio de creación de comprobación.");
            r.Payload = await servicioTranscript.ComprobacionPOSTAsync(request, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.CrearComprobacionExitoso", "Creación de comprobación completada.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.CrearComprobacionApiException", "Error API al crear comprobación.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearComprobacion"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.CrearComprobacionException", "Excepción no controlada al crear comprobación.", ex);
            r.Error = ex.ErrorGenerico("ServicioTranscript-CrearComprobacion");
        }
        return r;
    }

    public async Task<(byte[] Contenido, string TipoContenido)?> DescargarArchivoAsync(
        long id, string? tipo, CancellationToken ct = default)
    {
        try
        {
            _logger.Info("ServicioTranscript.DescargarArchivo", "Inicio de descarga de archivo de captura.");
            var url = $"captura/pagina/contenido/{id}";
            if (!string.IsNullOrEmpty(tipo) && tipo != "imagen")
                url += $"?tipo={Uri.EscapeDataString(tipo)}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Debug("ServicioTranscript.DescargarArchivoError", "Respuesta no exitosa al descargar archivo.", new Dictionary<string, object?>
                {
                    ["HttpCode"] = (int)response.StatusCode
                });
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType
                              ?? "application/octet-stream";
            _logger.Info("ServicioTranscript.DescargarArchivoExitoso", "Descarga de archivo completada.");
            return (bytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.DescargarArchivoException", "Excepción no controlada al descargar archivo.", ex);
            return null;
        }
    }

    public async Task<RespuestaPayload<ResumenCapturaCuentaFiscal>> GetEstadisticas(Guid cfid,int? anio,int? mes)
    {
        RespuestaPayload<ResumenCapturaCuentaFiscal> r = new();

        try
        {
            _logger.Info("ServicioTranscript.GetEstadisticas", "Inicio de consulta de estadísticas.");
            var res = await servicioTranscript.CuentafiscalAsync(cfid,null,anio,mes);
            if (res != null)
            {
                r.Payload = res;
            }
            r.Ok = true;
            _logger.Info("ServicioTranscript.GetEstadisticasExitoso", "Consulta de estadísticas completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.GetEstadisticasException", "Excepción no controlada al obtener estadísticas.", ex);
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
            _logger.Info("ServicioTranscript.CrearLote", "Inicio de creación de lote de captura.");
            r.Payload = await servicioTranscript.LotePOSTAsync(request, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.CrearLoteExitoso", "Creación de lote de captura completada.");
        }
        catch (ApiException ex) when (ex.StatusCode == 201)
        {
            _logger.Debug("ServicioTranscript.CrearLoteApi201", "API devolvió 201 vía excepción controlada al crear lote.", ex);
            r.Payload = JsonConvert.DeserializeObject<LoteCaptura>(ex.Response);
            r.Ok      = r.Payload is not null;
            if (!r.Ok)
                r.Error = new ErrorProceso { Mensaje = "Respuesta vacía al crear lote.", Origen = "ServicioTranscript-CrearLote" };
        }
        catch (ApiException ex) when (ex.StatusCode == 402)
        {
            _logger.Debug("ServicioTranscript.CrearLotePaymentRequired", "API devolvió PaymentRequired al crear lote.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = System.Net.HttpStatusCode.PaymentRequired,
                Origen   = "ServicioTranscript-CrearLote"
            };
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.CrearLoteApiException", "Error API al crear lote.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CrearLote"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.CrearLoteException", "Excepción no controlada al crear lote.", ex);
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
            _logger.Info("ServicioTranscript.ObtenerPrecarga", "Inicio de obtención de precarga de lote.");
            r.Payload = await servicioTranscript.PrecargaAsync(loteId, ct);
            r.Ok = true;
            _logger.Info("ServicioTranscript.ObtenerPrecargaExitoso", "Obtención de precarga de lote completada.");
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.ObtenerPrecargaException", "Excepción no controlada al obtener precarga de lote.", ex);
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
        _logger.Info("ServicioTranscript.SubirArchivosBlob", "Inicio de carga de archivos a blob.");
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
                _logger.Debug("ServicioTranscript.SubirArchivosBlobError", "Error HTTP al subir archivo.", new Dictionary<string, object?>
                {
                    ["FileName"] = fileName,
                    ["HttpCode"] = (int)response.StatusCode
                });
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

        _logger.Info("ServicioTranscript.SubirArchivosBlobExitoso", "Carga de archivos a blob completada.");
        return new Respuesta { Ok = true };
    }

    public async Task<Respuesta> CompletarLoteAsync(long loteId, DtoCierreLote? lote = null, CancellationToken ct = default)
    {
        Respuesta r = new();
        try
        {
            _logger.Info("ServicioTranscript.CompletarLote", "Inicio de cierre de lote de captura.");
            await servicioTranscript.Completar2Async(loteId, lote ?? new DtoCierreLote());
            r.Ok = true;
            _logger.Info("ServicioTranscript.CompletarLoteExitoso", "Cierre de lote de captura completado.");
        }
        catch (ApiException ex)
        {
            _logger.Debug("ServicioTranscript.CompletarLoteApiException", "Error API al cerrar lote de captura.", ex);
            r.Error = new ErrorProceso
            {
                Mensaje  = ex.Response,
                HttpCode = (System.Net.HttpStatusCode)ex.StatusCode,
                Origen   = "ServicioTranscript-CompletarLote"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("ServicioTranscript.CompletarLoteException", "Excepción no controlada al cerrar lote de captura.", ex);
            r.Error = ex.ErrorGenerico("ServicioTranscript-CompletarLote");
        }
        return r;
    }
}

