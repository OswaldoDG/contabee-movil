using System.Globalization;

namespace ContaBeeMovil.Converters;

public class DateTimeToLocalStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;
        var formato = parameter as string ?? "d/M/yyyy HH:mm";
        return dt.ToLocalTime().ToString(formato, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
