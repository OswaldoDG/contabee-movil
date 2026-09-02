namespace ContaBeeMovil.Services.IAP;

/// <summary>
/// Abre la hoja nativa de Apple para solicitar el reembolso de una compra.
/// La implementación solo está disponible en iOS 15 o posterior.
/// </summary>
public interface IServicioReembolsoApple
{
    string? ObtenerUltimaTransaccionId();
    bool RegistrarTransaccion(string? transaccionId);
    Task<ResultadoReembolsoApple> SolicitarAsync(
        string? transaccionId,
        CancellationToken cancellationToken = default);
}

public enum EstadoSolicitudReembolsoApple
{
    Enviada,
    Cancelada,
    NoDisponible,
    TransaccionInvalida,
    Error,
}

public sealed record ResultadoReembolsoApple(
    EstadoSolicitudReembolsoApple Estado,
    string? Detalle = null);
