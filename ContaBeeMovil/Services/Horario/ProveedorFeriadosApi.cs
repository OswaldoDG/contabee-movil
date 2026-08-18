using Contabee.Api.abstractions;
using ContaBeeMovil.Services.Dev;

namespace ContaBeeMovil.Services.Horario;

/// <summary>
/// Feriados leídos de <c>GET /captura/diasinhabiles</c> del servicio de transcript.
/// Sustituye a <see cref="ProveedorFeriadosVacio"/>.
/// </summary>
/// <remarks>
/// El backend usa este mismo catálogo para decidir su propio SLA de captura
/// (<c>ServicioColaProcesamiento.EsFeriadoMexico</c>), así que app y servidor coinciden
/// en qué día es hábil.
/// </remarks>
public sealed class ProveedorFeriadosApi : IProveedorFeriados
{
    /// <summary>Único país que atiende la app. El endpoint también lo asume por defecto.</summary>
    private const string CodigoPais = "MX";

    /// <summary>
    /// Cuánto hay que ver hacia adelante para decidir si además hace falta el año siguiente.
    /// Debe cubrir el horizonte que explora <see cref="ServicioHorarioCaptura"/> al buscar la
    /// próxima apertura (40 días), o un 31 de diciembre tomaríamos enero como todo hábil.
    /// </summary>
    private const int DiasAnticipacion = 45;

    private readonly IServicioTranscript _transcript;
    private readonly IServicioLogs _logs;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<int, HashSet<DateOnly>> _porAno = [];

    /// <summary>
    /// Vista plana de todos los años cargados. Se reemplaza completa, nunca se muta: así
    /// <see cref="EsFeriado"/> puede leerla desde el hilo de UI sin lock y sin riesgo de
    /// ver un conjunto a medio llenar.
    /// </summary>
    private volatile IReadOnlySet<DateOnly> _feriados = new HashSet<DateOnly>();

    public ProveedorFeriadosApi(IServicioTranscript transcript, IServicioLogs logs)
    {
        _transcript = transcript;
        _logs = logs;
    }

    public bool EsFeriado(DateOnly fechaCentral) => _feriados.Contains(fechaCentral);

    public async Task PrecargarAsync(CancellationToken ct = default)
    {
        // Un año ya cargado no se vuelve a pedir en esta sesión: el catálogo es estático y
        // la precarga se dispara cada vez que se abre la página de captura.
        var pendientes = AnosNecesarios().Where(a => !_porAno.ContainsKey(a)).ToArray();
        if (pendientes.Length == 0) return;

        await _lock.WaitAsync(ct);
        try
        {
            bool huboCambios = false;

            foreach (var ano in pendientes)
            {
                if (_porAno.ContainsKey(ano)) continue;   // otro hilo lo cargó mientras esperábamos

                var respuesta = await _transcript.ObtenerDiasInhabilesAsync(CodigoPais, ano, ct);

                if (!respuesta.Ok)
                {
                    // Sin feriados sólo perdemos precisión: el horario sigue funcionando con
                    // la regla de lunes a viernes. No se cachea el fallo, se reintenta luego.
                    _logs.Warn($"[Feriados] No se pudo cargar {ano}: HTTP {(int)respuesta.HttpCode} {respuesta.Error?.Mensaje}");
                    continue;
                }

                _porAno[ano] = ConvertirAFechas(respuesta.Payload);
                huboCambios = true;
                _logs.Info($"[Feriados] {ano}: {_porAno[ano].Count} día(s) inhábil(es)");
            }

            if (huboCambios)
                _feriados = _porAno.Values.SelectMany(s => s).ToHashSet();
        }
        catch (Exception ex)
        {
            // Contrato de IProveedorFeriados: precargar nunca lanza. Conservamos lo que haya.
            _logs.Warn($"[Feriados] Precarga interrumpida: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// El año en curso y, si el horizonte de búsqueda cruza el 31 de diciembre, el siguiente.
    /// </summary>
    private static int[] AnosNecesarios()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var limite = hoy.AddDays(DiasAnticipacion);

        return limite.Year != hoy.Year ? [hoy.Year, limite.Year] : [hoy.Year];
    }

    private HashSet<DateOnly> ConvertirAFechas(ICollection<Contabee.Api.Transcript.DiaInhabil>? dias)
    {
        var set = new HashSet<DateOnly>();
        if (dias is null) return set;

        foreach (var d in dias)
        {
            // Una fila corrupta (mes 0, 31 de febrero) no debe tumbar el horario completo.
            if (d.Ano is < 1 or > 9999 || d.Mes is < 1 or > 12 ||
                d.Dia < 1 || d.Dia > DateTime.DaysInMonth(d.Ano, d.Mes))
            {
                _logs.Warn($"[Feriados] Fecha inválida ignorada: {d.Ano}-{d.Mes}-{d.Dia} ({d.Motivo})");
                continue;
            }

            set.Add(new DateOnly(d.Ano, d.Mes, d.Dia));
        }

        return set;
    }
}
