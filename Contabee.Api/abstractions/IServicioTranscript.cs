

using Contabee.Api.Crm;
using Contabee.Api.Transcript;

namespace Contabee.Api.abstractions;

public interface IServicioTranscript
{
    Task<ResultadoPaginado_1OfOfElementoPaginaCapturaDespliegueAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaCapturas(Busqueda consulta);

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
