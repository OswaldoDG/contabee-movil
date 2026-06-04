using System.Globalization;

namespace ContaBeeMovil.Converters;

public class DateTimeToLocalStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var formato = parameter as string ?? "d/M/yyyy HH:mm";

        if (value is DateTimeOffset dto)
        {
            // El servidor devuelve la hora UTC pero con el offset local incorrecto
            // (ej. 19:22-06:00 en vez de 19:22+00:00 ó 13:22-06:00).
            // Tratamos el valor DateTime crudo como UTC y convertimos a hora local.
            var utc = DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString(formato, culture);
        }

        if (value is DateTime dt)
            return dt.ToLocalTime().ToString(formato, culture);

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
