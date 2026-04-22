using System.Globalization;

namespace ContaBeeMovil.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Colors.White 
                : Colors.Black;
        }
        
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
