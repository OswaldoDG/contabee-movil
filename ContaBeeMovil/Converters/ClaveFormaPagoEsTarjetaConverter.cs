using System.Globalization;

namespace ContaBeeMovil.Converters;

public class ClaveFormaPagoEsTarjetaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string clave && clave is "04" or "4" or "28";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
