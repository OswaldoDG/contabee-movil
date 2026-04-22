using System.Globalization;

namespace ContaBeeMovil.Converters;

public class NombreInicialesConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string nombre || string.IsNullOrWhiteSpace(nombre))
            return string.Empty;

        var palabras = nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (palabras.Length == 0)
            return string.Empty;

        var iniciales = string.Join("", palabras.Select(p => char.ToUpper(p[0])));
        return iniciales;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
