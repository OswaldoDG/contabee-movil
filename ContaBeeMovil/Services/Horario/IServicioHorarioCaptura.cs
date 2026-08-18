namespace ContaBeeMovil.Services.Horario;

/// <summary>
/// Resultado de evaluar el horario de captura en un instante dado.
/// </summary>
/// <param name="Abierto">True si en este momento ContaBee está capturando.</param>
/// <param name="AhoraCentral">El instante evaluado, ya convertido a hora central de México.</param>
/// <param name="ProximaAperturaCentral">
/// Cuándo se reanuda la captura (hora central). Null cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="Mensaje">
/// Leyenda lista para mostrar al usuario. Es la misma en las tres vistas que la usan
/// (aviso de la página sin fotos, popup de la franja y tarjeta del selector "Quién
/// captura"), y por eso está redactada en plural y sin condicional: en dos de esas tres
/// el usuario no está enviando nada — en el aviso de la página ni siquiera ha tomado la
/// foto. Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="ResumenCorto">
/// Versión mínima del aviso para espacios estrechos (p. ej. "reanuda lun 9:00").
/// Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="MensajeBreve">
/// Una línea para la franja de estado (p. ej. "Fuera de horario · Se procesará el
/// lunes 9:00 a.m."). Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
public sealed record EstadoHorarioCaptura(
    bool Abierto,
    DateTime AhoraCentral,
    DateTime? ProximaAperturaCentral,
    string Mensaje,
    string ResumenCorto,
    string MensajeBreve);

/// <summary>
/// Reglas del horario en que ContaBee realiza la captura delegada de tickets.
/// </summary>
/// <remarks>
/// Aplica sólo al crédito de <b>Captura</b> (ContaBee captura por el usuario). El
/// crédito de <b>Autoservicio</b> lo captura el propio usuario, así que no depende
/// de este horario.
/// </remarks>
public interface IServicioHorarioCaptura
{
    /// <summary>
    /// Evalúa el horario. <paramref name="momento"/> existe para pruebas; en producción
    /// se omite y se usa la hora actual.
    /// </summary>
    EstadoHorarioCaptura ObtenerEstado(DateTimeOffset? momento = null);

    /// <summary>Precarga el catálogo de feriados. Nunca lanza.</summary>
    Task PrecargarFeriadosAsync(CancellationToken ct = default);
}
