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
                                            string.Empty, string.Empty, string.Empty);

        var proxima = CalcularProximaApertura(ahoraCentral);
        return new EstadoHorarioCaptura(false, ahoraCentral, proxima,
                                        ConstruirMensaje(ahoraCentral, proxima),
                                        ConstruirResumenCorto(ahoraCentral, proxima),
                                        ConstruirMensajeBreve(ahoraCentral, proxima));
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

    private static string ConstruirMensaje(DateTime ahoraCentral, DateTime proximaApertura)
        => $"Las actividades de captura de Contabee reiniciarán {DescribirApertura(ahoraCentral, proximaApertura)} " +
            "y haremos nuestro mejor esfuerzo por tenerlas listas a la brevedad.";

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
    /// Franja de estado. Redacción deliberada: <b>"a partir de"</b>, no "se procesará
    /// el/a las". La segunda forma se lee como un compromiso de que el envío de ESE
    /// usuario queda listo a esa hora exacta, y lo que ocurre es que a esa hora
    /// arranca el procesamiento de la cola. Sujeto en plural ("los envíos") por lo
    /// mismo: habla de cuándo reanuda la operación, no de un caso particular.
    /// </summary>
    private static string ConstruirMensajeBreve(DateTime ahoraCentral, DateTime proximaApertura)
        => $"Procesamos los envíos a partir {DescribirAperturaDesde(ahoraCentral, proximaApertura)}";

    /// <summary>
    /// Complemento de "a partir …": "de hoy 9:00 a.m." / "de mañana …" / "del lunes …".
    /// Sin "a las" ni "el próximo" — la franja es de una sola línea y esos caracteres
    /// son la diferencia entre que quepa o se trunque.
    /// </summary>
    private static string DescribirAperturaDesde(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy  = DateOnly.FromDateTime(ahoraCentral);
        var dia  = DateOnly.FromDateTime(proximaApertura);
        var hora = FormatearHora(proximaApertura);

        if (dia == hoy)            return $"de hoy {hora}";
        if (dia == hoy.AddDays(1)) return $"de mañana {hora}";

        return $"del {NombreDia(dia.DayOfWeek)} {hora}";
    }

    private static string DescribirApertura(DateTime ahoraCentral, DateTime proximaApertura)
    {
        var hoy  = DateOnly.FromDateTime(ahoraCentral);
        var dia  = DateOnly.FromDateTime(proximaApertura);
        var hora = FormatearHora(proximaApertura);

        if (dia == hoy)             return $"hoy a las {hora}";
        if (dia == hoy.AddDays(1))  return $"mañana a las {hora}";

        var cuando = $"el próximo {NombreDia(dia.DayOfWeek)}";
        // Más allá de la semana en curso el nombre del día ya no basta para ubicarlo.
        if (dia.DayNumber - hoy.DayNumber > 6)
            cuando += $" {dia.Day} de {NombreMes(dia.Month)}";

        return $"{cuando} a las {hora}";
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
