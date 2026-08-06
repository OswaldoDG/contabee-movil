namespace ContaBeeMovil.Services.Horario;

/// <summary>
/// Fuente de días feriados (no hábiles) para el cálculo del horario de captura.
/// </summary>
/// <remarks>
/// Hoy se registra <see cref="ProveedorFeriadosVacio"/>: no hay feriados y el horario
/// depende sólo del día de la semana y de los últimos tres días del mes.
/// Cuando el endpoint de feriados llegue al backend, basta con implementar esta
/// interfaz contra el cliente NSwag y cambiar el registro en MauiProgram — ni
/// <see cref="ServicioHorarioCaptura"/> ni la página necesitan cambios.
/// </remarks>
public interface IProveedorFeriados
{
    /// <summary>
    /// True si <paramref name="fechaCentral"/> (fecha en hora central de México) es feriado.
    /// Debe resolver en memoria: se consulta desde el hilo de UI en cada refresco.
    /// </summary>
    bool EsFeriado(DateOnly fechaCentral);

    /// <summary>
    /// Precarga/refresca el catálogo de feriados. Nunca debe lanzar: si falla, el
    /// proveedor sigue respondiendo con lo que tenga en caché.
    /// </summary>
    Task PrecargarAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementación temporal: sin feriados. Ver <see cref="IProveedorFeriados"/>.
/// </summary>
public sealed class ProveedorFeriadosVacio : IProveedorFeriados
{
    public bool EsFeriado(DateOnly fechaCentral) => false;

    public Task PrecargarAsync(CancellationToken ct = default) => Task.CompletedTask;
}
