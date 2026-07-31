namespace ContaBeeMovil.Helpers;

/// <summary>
/// Construcción de rangos de fecha para los filtros de búsqueda.
/// </summary>
public static class RangosFecha
{
    /// <summary>
    /// Offset de la hora local de México respecto a UTC (UTC-6). La medianoche local
    /// del día D equivale a las 06:00Z del mismo día D.
    /// </summary>
    private const int HorasOffsetLocal = 6;

    /// <summary>
    /// Rango UTC que cubre el mes local completo (del día 1 a las 00:00 locales hasta
    /// el último instante del último día). El backend traduce el operador
    /// <c>Entre</c> a un <c>BETWEEN</c> SQL inclusivo en ambos extremos, por eso el
    /// límite superior se cierra en 05:59:59.999Z del día 1 del mes siguiente: así el
    /// último día del mes queda dentro y no se traslapa con el mes que sigue.
    /// </summary>
    public static (string Inicio, string Fin) RangoUtcDelMes(int anio, int mes)
    {
        var inicioUtc = new DateTime(anio, mes, 1, HorasOffsetLocal, 0, 0, DateTimeKind.Utc);
        var finUtc = inicioUtc.AddMonths(1).AddMilliseconds(-1);

        return (Formatear(inicioUtc), Formatear(finUtc));
    }

    private static string Formatear(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss.fff") + "Z";
}
