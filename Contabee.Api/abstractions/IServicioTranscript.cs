

using Contabee.Api.Crm;
using Contabee.Api.Transcript;

namespace Contabee.Api.abstractions;

public interface IServicioTranscript
{
    Task<ResultadoPaginado_1OfOfElementoPaginaCapturaDespliegueAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaCapturas(Busqueda consulta);

    Task<ResultadoPaginado_1OfOfComprobacionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaComprobaciones(Busqueda consulta);

    Task<ResultadoPaginado_1OfOfDevolucionAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaDevoluciones(Busqueda consulta);

    Task<RespuestaPayload<Comprobacion>> CrearComprobacionAsync(
        CreaComprobacion request, CancellationToken ct = default);

    Task<RespuestaPayload<Devolucion>> CrearDevolucionAsync(
        CreaDevolucion request, CancellationToken ct = default);

    Task<RespuestaPayload<Comprobacion>> ObtenerComprobacionAsync(
        Guid id, CancellationToken ct = default);

    Task<RespuestaPayload<Devolucion>> ObtenerDevolucionAsync(
        Guid id, CancellationToken ct = default);

    Task<RespuestaPayload<Comprobacion>> ActualizarComprobacionAsync(
        Guid id, ActualizaComprobacion request, CancellationToken ct = default);

    Task<RespuestaPayload<Devolucion>> ActualizarDevolucionAsync(
        Guid id, ActualizaDevolucion request, CancellationToken ct = default);

    Task<RespuestaPayload<Comprobacion>> ActualizarEstadoComprobacionAsync(
        Guid id, EstadoComprobacion estado, CancellationToken ct = default);

    Task<RespuestaPayload<Devolucion>> ActualizarEstadoDevolucionAsync(
        Guid id, EstadoDevolucion estado, CancellationToken ct = default);

    Task<Respuesta> EliminarComprobacionAsync(
        Guid id, CancellationToken ct = default);

    Task<Respuesta> EliminarDevolucionAsync(
        Guid id, CancellationToken ct = default);

    Task<(byte[] Contenido, string TipoContenido)?> DescargarArchivoAsync(
        long id, string? tipo, CancellationToken ct = default);

    Task<RespuestaPayload<ResumenCapturaCuentaFiscal>> GetEstadisticas(Guid cfid, int? anio, int? mes);

    Task<RespuestaPayload<LoteCaptura>> CrearLoteAsync(
        CreaLoteCaptura request, CancellationToken ct = default);

    Task<RespuestaPayload<DtoLoteCapturaCreado>> ObtenerPrecargaAsync(
        long loteId, CancellationToken ct = default);

    /// <summary>
    /// Sube archivos directamente al Azure Blob Storage usando el SAS token del lote.
    /// Cada archivo recibe un índice consecutivo (001, 002, …) como nombre.
    /// </summary>
    Task<Respuesta> SubirArchivosBlobAsync(
        string sasToken,
        IReadOnlyList<string> rutasArchivos,
        IProgress<double>? progreso = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cierra el ciclo del lote. Debe llamarse siempre que el lote fue creado,
    /// independientemente de si los pasos anteriores tuvieron éxito o no.
    /// </summary>
    Task<Respuesta> CompletarLoteAsync(long loteId, CancellationToken ct = default);
}
