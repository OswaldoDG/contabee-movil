using MauiIcons.Material;
using System.Globalization;

namespace ContaBeeMovil.Converters;

public class ClaveFormaPagoIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string clave
            ? clave switch
            {
                "04" or "4" or "28" => MaterialIcons.CreditCard,
                "03" or "3"         => MaterialIcons.Language,
                _                   => MaterialIcons.Payments
            }
            : MaterialIcons.Payments;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
