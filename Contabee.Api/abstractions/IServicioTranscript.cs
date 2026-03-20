

using Contabee.Api.Crm;
using Contabee.Api.Transcript;

namespace Contabee.Api.abstractions;

public interface IServicioTranscript
{
    Task<ResultadoPaginado_1OfOfElementoPaginaCapturaDespliegueAndTranscriptAnd_0AndCulture_neutralAndPublicKeyToken_null> BusquedaCapturas(Busqueda consulta);

    Task<(byte[] Contenido, string TipoContenido)?> DescargarArchivoAsync(
        long id, string? tipo, CancellationToken ct = default);
}
