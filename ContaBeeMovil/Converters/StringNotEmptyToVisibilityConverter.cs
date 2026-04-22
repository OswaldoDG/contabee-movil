using System.Globalization;

namespace ContaBeeMovil.Converters;

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string texto && !string.IsNullOrWhiteSpace(texto))
            return true;
        
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
