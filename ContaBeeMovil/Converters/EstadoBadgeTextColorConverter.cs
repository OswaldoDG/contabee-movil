using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

/// <summary>
/// Color del texto del badge central:
///   ≤4 (pendiente)  → Blanco
///   5  (finalizado) → PrimaryText (negro en light, blanco en dark)
///   6  (error)      → Blanco
/// </summary>
public class EstadoBadgeTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return Colors.White;

        return (int)item.Estado switch
        {
            <= 4 => Colors.White,
            5 => EstadoBadgeColorConverter.ResolveColor("PrimaryText", Colors.Black),
            6 => Colors.White,
            _ => Colors.White
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
