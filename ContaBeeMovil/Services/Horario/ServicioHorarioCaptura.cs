using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Services.Horario;

/// <summary>
/// Horario de captura delegada de ContaBee:
/// <list type="bullet">
///   <item>Lunes a viernes de 9:00 a 18:00 hora central, si no es día feriado.</item>
///   <item>En los últimos tres días del mes, de 9:00 a 18:00 <b>siempre</b> — aunque
///         caiga en fin de semana o en feriado (es el cierre mensual).</item>
/// </list>
/// Todo se evalúa en hora central de México, no en la hora del dispositivo: el usuario
/// puede estar viajando o tener mal configurada la zona horaria del teléfono.
/// </summary>
public sealed class ServicioHorarioCaptura : IServicioHorarioCaptura
{
    private static readonly TimeSpan HoraApertura = new(9, 0, 0);
    private static readonly TimeSpan HoraCierre   = new(18, 0, 0);

    /// <summary>Cuántos días finales del mes son hábiles sin importar qué día caigan.</summary>
    private const int DiasCierreMensual = 3;

    private readonly IProveedorFeriados _feriados;
    private readonly IServicioLogs _logs;
    private readonly TimeZoneInfo _zonaCentral;

    public ServicioHorarioCaptura(IProveedorFeriados feriados, IServicioLogs logs)
    {
        _feriados    = feriados;
        _logs        = logs;
        _zonaCentral = ResolverZonaCentral(logs);
    }

    /// <summary>
    /// Hora central simulada para poder ver el aviso sin esperar a que sea de noche o
    /// fin de semana. En null (lo normal) se usa la hora real.
    /// </summary>
    /// <remarks>
    /// No está detrás de <c>#if DEBUG</c> a propósito: las pruebas se hacen en celular
    /// con builds normales, donde ese símbolo no existe. El único que la escribe es
    /// <c>PaginaCaptura</c>, y sólo con el Modo Desarrollador activo — para un usuario
    /// real siempre vale null.
    /// </remarks>
    public static DateTime? MomentoSimuladoCentral { get; set; }

    public EstadoHorarioCaptura ObtenerEstado(DateTimeOffset? momento = null)
    {
        var ahoraCentral = momento is not null
            ? TimeZoneInfo.ConvertTime(momento.Value, _zonaCentral).DateTime
            : MomentoSimuladoCentral
              ?? TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _zonaCentral).DateTime;

        var fechaHoy     = DateOnly.FromDateTime(ahoraCentral);

        var abierto = EsDiaHabil(fechaHoy)
                      && ahoraCentral.TimeOfDay >= HoraApertura
                      && ahoraCentral.TimeOfDay <  HoraCierre;

        if (abierto)
            return new EstadoHorarioCaptura(true, ahoraCentral, null,
                                            string.Empty, string.Empty, string.Empty,
                                            string.Empty, string.Empty);

        var proxima = CalcularProximaApertura(ahoraCentral);
        return new EstadoHorarioCaptura(false, ahoraCentral, proxima,
                                        ConstruirMensaje(ahoraCentral, proxima),
                                        ConstruirResumenCorto(ahoraCentral, proxima),
                                        ConstruirMensajeBreve(ahoraCentral, proxima),
                                        ConstruirMensajeFranja(ahoraCentral, proxima),
                                        ConstruirMensajeFranjaUrgente(ahoraCentral, proxima));
    }

    public async Task PrecargarFeriadosAsync(CancellationToken ct = default)
    {
        try
        {
            await _feriados.PrecargarAsync(ct);
        }
        catch (Exception ex)
        {
            // El horario sigue siendo utilizable sin feriados: sólo perdemos precisión.
            _logs.Warn($"[HorarioCaptura] No se pudieron precargar los feriados: {ex.Message}");
        }
    }

    // ── Reglas ───────────────────────────────────────────────────────────────

    private bool EsDiaHabil(DateOnly fecha)
        => EsCierreMensual(fecha)
           || (fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !_feriados.EsFeriado(fecha));

    private static bool EsCierreMensual(DateOnly fecha)
        => fecha.Day > DateTime.DaysInMonth(fecha.Year, fecha.Month) - DiasCierreMensual;

    private DateTime CalcularProximaApertura(DateTime ahoraCentral)
    {
        var hoy = DateOnly.FromDateTime(ahoraCentral);

        // Hoy es hábil pero todavía no dan las 9: la apertura es hoy mismo.
        if (EsDiaHabil(hoy) && ahoraCentral.TimeOfDay < HoraApertura)
            return hoy.ToDateTime(TimeOnly.FromTimeSpan(HoraApertura));

        // Si no, el siguiente día hábil. El cierre mensual garantiza que siempre hay
        // uno dentro del mes; el tope es una red de seguridad ante un catálogo de
        // feriados corrupto que marcara todo el calendario.
        for (var i = 1; i <= 40; i++)
        {
            var fecha = hoy.AddDays(i);
            if (EsDiaHabil(fecha))
                return fecha.ToDateTime(TimeOnly.FromTimeSpan(HoraApertura));
        }

        _logs.Warn("[HorarioCaptura] No se encontró día hábil en 40 días; se usa el día siguiente.");
        return hoy.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(HoraApertura));
    }

    // ── Mensaje ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Aviso completo. Lo que le importa al usuario no es a qué hora vuelve ContaBee,
    /// sino qué le pasa a lo que envíe <b>ahora</b>: se recibe igual y se procesa en
    /// cuanto se reanuda. Por eso el sujeto son sus capturas y no nuestro horario.
    /// </summary>
    /// <remarks>
    /// Redacción dada por negocio. Tres cosas que parecen detalle y no lo son:
    /// · El "las" de "las procesaremos" se apoya en el título de la vista que lo muestra
    ///   ("Sigue enviando tus fotos"). Si alguna vista futura lo pinta sin ese título, el
    ///   pronombre queda sin antecedente y hay que darle uno.
    /// · "A primera hora" y no la hora exacta: el compromiso es de prontitud, no de reloj.
    ///   La hora concreta sí aparece en <see cref="ConstruirMensajeBreve"/>, que es una
    ///   franja de estado y ahí sí se espera un dato duro.
    /// · Plural y sin condicional a propósito. De las tres vistas que lo muestran sólo el
    ///   selector "Quién captura" aparece con un envío en curso; en las otras dos un "si
    ///   continúas…" le hablaría al usuario de un envío que no está haciendo.
    /// </remarks>
    private static string ConstruirMensaje(DateTime ahoraCentral, DateTime proximaApertura)
        => "Estamos fuera de horario hábil pero las procesaremos a la brevedad " +
           $"{DescribirAperturaPrimeraHora(ahoraCentral, proximaApertura)}, o antes si es posible.";

    /// <summary>
    /// Variante mínima para huecos estrechos (el flyout "Quién captura"): "reanuda lun 9:00".
    /// </summary>
    private static string ConstruirResumenCorto(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy = DateOnly.FromDateTime(ahoraCentral);
        var dia = DateOnly.FromDateTime(proximaApertura);

        var cuando = dia == hoy            ? "hoy"
                   : dia == hoy.AddDays(1) ? "mañana"
                   : NombreDia(dia.DayOfWeek)[..3];

        return $"reanuda {cuando} {proximaApertura.Hour}:{proximaApertura.Minute:00}";
    }

    /// <summary>
    /// Franja de estado: "Se procesará el lunes 9:00 a.m.". Sin prefijo de "fuera de
    /// horario" — la franja sólo aparece cuando ya se está fuera, y el reloj que la
    /// acompaña en la UI da el contexto.
    /// </summary>
    /// <remarks>
    /// Nota de redacción, por si alguien la revisa a futuro: "se procesará" en singular
    /// puede leerse como que el envío de ESE usuario queda listo a esa hora exacta,
    /// cuando lo que ocurre a esa hora es que arranca el procesamiento de la cola. Se
    /// eligió así de forma deliberada por ser más directa; si en soporte aparecen
    /// reclamos de "dijeron que a las 9 estaría", el cambio a hacer es volver a un
    /// sujeto en plural del tipo "Procesamos los envíos a partir de…".
    /// </remarks>
    private static string ConstruirMensajeBreve(DateTime ahoraCentral, DateTime proximaApertura)
        => $"Se procesará {DescribirAperturaProceso(ahoraCentral, proximaApertura)}";

    /// <summary>
    /// Aviso en una línea para la franja fija de la página de captura:
    /// "¡Envía tus capturas! Procesamos todo desde las 9:00 a.m.".
    /// </summary>
    /// <remarks>
    /// Arranca con la acción y NO con "Fuera de horario", que es como estuvo primero. La
    /// franja vive pegada a la barra de botones, o sea que se lee con el pulgar sobre
    /// Enviar, y en ese instante lo que importa es qué hacer, no en qué estado está el
    /// servicio.
    /// <para>
    /// Se pierde el dato explícito de estar fuera de horario; lo carga implícito el
    /// "desde las 9:00 a.m.", y la franja sólo existe cuando se está fuera. Si en soporte
    /// aparecen dudas de "¿por qué me sale esto?", es lo primero a reconsiderar.
    /// </para>
    /// <para>
    /// "Todo" en vez de un pronombre esquiva el problema que sí tiene
    /// <see cref="ConstruirMensaje"/>, cuyo "las procesaremos" necesita un título encima
    /// del que tomar antecedente. Aquí no hay título del que colgarse.
    /// </para>
    /// </remarks>
    private static string ConstruirMensajeFranja(DateTime ahoraCentral, DateTime proximaApertura)
        => $"¡Envía tus capturas! Procesamos todo {DescribirAperturaFranja(ahoraCentral, proximaApertura)}";

    /// <summary>
    /// La franja cuando el lote va marcado como <b>Urgente</b>: "Tu envío urgente será el
    /// primero en atenderse al reanudar, el lunes 9:00 a.m.".
    /// </summary>
    /// <remarks>
    /// Es la única variante que NO empieza por la acción, y con motivo: a quien ya marcó
    /// Urgente no hay que animarlo a enviar — lo que necesita saber es qué le compró esa
    /// marca estando fuera de horario. La respuesta es el lugar en la cola, no que se
    /// procese antes de las 9.
    /// <para>
    /// "Será el primero" es un compromiso de ORDEN, no de hora, y así tiene que quedarse.
    /// Redactado como que se atiende "de inmediato" o "antes" prometería algo que el
    /// horario no puede cumplir: fuera de horario no hay nadie capturando.
    /// </para>
    /// <para>
    /// Usa <see cref="DescribirAperturaProceso"/> y no <see cref="DescribirAperturaFranja"/>
    /// porque aquí la fecha va detrás de una coma y no de un verbo: "al reanudar, el lunes
    /// 9:00 a.m.". Con la otra saldría "al reanudar, desde el lunes…", que dice dos veces
    /// lo mismo.
    /// </para>
    /// </remarks>
    private static string ConstruirMensajeFranjaUrgente(DateTime ahoraCentral, DateTime proximaApertura)
        => "Tu envío urgente será el primero en atenderse al reanudar, " +
           $"{DescribirAperturaProceso(ahoraCentral, proximaApertura)}";

    /// <summary>
    /// Complemento de "…procesamos": "desde las 9:00 a.m." / "desde mañana …" /
    /// "desde el lunes …".
    /// </summary>
    /// <remarks>
    /// Es la única de las tres formas de describir la apertura que lleva preposición,
    /// porque es la única que va detrás de un verbo. Cuando la reanudación es HOY se
    /// omite el día — "procesamos desde hoy 9:00 a.m." suena a que empieza una etapa, y
    /// lo que se quiere decir es una hora.
    /// </remarks>
    private static string DescribirAperturaFranja(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy  = DateOnly.FromDateTime(ahoraCentral);
        var dia  = DateOnly.FromDateTime(proximaApertura);
        var hora = FormatearHora(proximaApertura);

        if (dia == hoy)            return $"desde las {hora}";
        if (dia == hoy.AddDays(1)) return $"desde mañana {hora}";

        return $"desde el {NombreDia(dia.DayOfWeek)} {hora}";
    }

    /// <summary>
    /// Complemento de "Se procesará …": "hoy 9:00 a.m." / "mañana …" / "el lunes …".
    /// El artículo va sólo con el nombre del día — "el hoy" / "el mañana" no se dicen.
    /// Sin "a las" ni "el próximo": la franja es de una sola línea y esos caracteres
    /// son la diferencia entre que quepa o se trunque.
    /// </summary>
    private static string DescribirAperturaProceso(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy  = DateOnly.FromDateTime(ahoraCentral);
        var dia  = DateOnly.FromDateTime(proximaApertura);
        var hora = FormatearHora(proximaApertura);

        if (dia == hoy)            return $"hoy {hora}";
        if (dia == hoy.AddDays(1)) return $"mañana {hora}";

        return $"el {NombreDia(dia.DayOfWeek)} {hora}";
    }

    /// <summary>
    /// Complemento de "…a la brevedad": "hoy a primera hora" / "mañana a primera hora" /
    /// "el próximo lunes a primera hora".
    /// </summary>
    /// <remarks>
    /// La regla de negocio se enunció como "menos de 24 horas → mañana; más de 24 horas →
    /// el día de la semana", pero se implementa por <b>día de calendario</b>, que es lo
    /// que el usuario lee en el reloj. Las dos formas coinciden en todos los casos salvo
    /// uno, y ahí la de 24 horas se equivoca: a las 7:00 de un martes la reanudación es
    /// ese mismo martes a las 9:00 — faltan 2 horas, o sea "menos de 24", pero decir
    /// "mañana" sería falso. Por eso existe la rama "hoy", que la regla original no
    /// contempla porque sólo pensaba en el caso de después de las 18:00.
    /// </remarks>
    private static string DescribirAperturaPrimeraHora(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy = DateOnly.FromDateTime(ahoraCentral);
        var dia = DateOnly.FromDateTime(proximaApertura);

        if (dia == hoy)            return "hoy a primera hora";
        if (dia == hoy.AddDays(1)) return "mañana a primera hora";

        var cuando = $"el próximo {NombreDia(dia.DayOfWeek)}";
        // Más allá de la semana en curso el nombre del día ya no basta para ubicarlo.
        // Hoy es inalcanzable (el cierre mensual garantiza un día hábil dentro del mes);
        // queda como red de seguridad si algún día cambian las reglas o los feriados.
        if (dia.DayNumber - hoy.DayNumber > 6)
            cuando += $" {dia.Day} de {NombreMes(dia.Month)}";

        return $"{cuando} a primera hora";
    }

    // Nombres fijos en español: la app es sólo para México y así el texto no depende
    // de que la plataforma tenga los datos ICU de es-MX cargados.
    private static string NombreDia(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday    => "lunes",
        DayOfWeek.Tuesday   => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday  => "jueves",
        DayOfWeek.Friday    => "viernes",
        DayOfWeek.Saturday  => "sábado",
        _                   => "domingo",
    };

    private static string NombreMes(int mes) => mes switch
    {
        1  => "enero",   2  => "febrero",   3  => "marzo",      4  => "abril",
        5  => "mayo",    6  => "junio",     7  => "julio",      8  => "agosto",
        9  => "septiembre", 10 => "octubre", 11 => "noviembre", _ => "diciembre",
    };

    private static string FormatearHora(DateTime momento)
    {
        var hora12 = momento.Hour % 12 == 0 ? 12 : momento.Hour % 12;
        var sufijo = momento.Hour < 12 ? "a.m." : "p.m.";
        return momento.Minute == 0
            ? $"{hora12}:00 {sufijo}"
            : $"{hora12}:{momento.Minute:00} {sufijo}";
    }

    // ── Zona horaria ─────────────────────────────────────────────────────────

    private static TimeZoneInfo ResolverZonaCentral(IServicioLogs logs)
    {
        // IANA (Android/iOS) y Windows usan ids distintos para la misma zona.
        foreach (var id in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception) { /* se prueba el siguiente id */ }
        }

        // Último recurso: México dejó el horario de verano en 2022, así que el centro
        // del país es UTC-6 todo el año.
        logs.Warn("[HorarioCaptura] Zona central no encontrada en el sistema; se usa UTC-6 fijo.");
        return TimeZoneInfo.CreateCustomTimeZone("Contabee/CentroMexico", TimeSpan.FromHours(-6),
                                                 "Centro de México", "Centro de México");
    }
}
