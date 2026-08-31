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
/// (aviso de la página sin fotos, coach mark de la mascota y tarjeta del selector "Quién
/// captura"), y por eso está redactada en plural y sin condicional: en dos de esas tres
/// el usuario no está enviando nada — en el aviso de la página ni siquiera ha tomado la
/// foto. <b>Da por hecho que la vista lleva encima el título "Sigue enviando tus fotos"</b>:
/// de ahí toma su antecedente el "las" de "las procesaremos". Cadena vacía cuando
/// <paramref name="Abierto"/> es true.
/// </param>
/// <param name="ResumenCorto">
/// Versión mínima del aviso para espacios estrechos (p. ej. "reanuda lun 9:00").
/// Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="MensajeBreve">
/// Una línea para la franja de estado (p. ej. "Se procesará el lunes 9:00 a.m.").
/// Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="MensajeFranja">
/// El aviso en una línea para la franja fija que va sobre la barra de botones de la
/// página de captura (p. ej. "Sigue enviando: los procesamos desde las 9:00 a.m.").
/// A diferencia de <paramref name="MensajeBreve"/> se basta solo, porque no lleva encima
/// ningún título del que tomar contexto. Empieza por la acción y no por el estado: se lee
/// con el pulgar sobre Enviar. Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
/// <param name="MensajeFranjaUrgente">
/// La misma franja cuando el lote va marcado como <b>Urgente</b> (p. ej. "Tu envío
/// urgente será el primero en atenderse al reanudar, el lunes 9:00 a.m."). Se calculan
/// las dos variantes de una vez y elige la vista, porque el servicio no sabe —ni tiene
/// por qué— qué opciones lleva marcadas el lote.
/// Cadena vacía cuando <paramref name="Abierto"/> es true.
/// </param>
public sealed record EstadoHorarioCaptura(
    bool Abierto,
    DateTime AhoraCentral,
    DateTime? ProximaAperturaCentral,
    string Mensaje,
    string ResumenCorto,
    string MensajeBreve,
    string MensajeFranja,
    string MensajeFranjaUrgente);

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
