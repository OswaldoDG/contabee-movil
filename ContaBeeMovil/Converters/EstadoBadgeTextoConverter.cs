using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

/// <summary>
/// Badge central de la card:
///   ≤4  → "Pendiente" / "En proceso"
///   5   → Total formateado como moneda
///   6   → "No valida"
/// </summary>
public class EstadoBadgeTextoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return string.Empty;

        return (int)item.Estado switch
        {
            <= 2 => "Pendiente",
            <= 4 => "En proceso",
            5    => item.Total.HasValue
                        ? item.Total.Value.ToString("C2", new CultureInfo("es-MX"))
                        : "Pendiente",
            6    => "No valida",
            _    => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
