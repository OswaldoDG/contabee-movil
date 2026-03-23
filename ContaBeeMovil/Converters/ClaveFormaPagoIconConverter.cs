using System.Globalization;

namespace ContaBeeMovil.Converters;

public class ClaveFormaPagoIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string clave
            ? clave switch
            {
                "04" or "4" or "28" => Fonts.FluentUI.wallet_credit_card_20_regular,
                "03" or "3"         => Fonts.FluentUI.globe_20_regular,
                _                   => Fonts.FluentUI.money_hand_20_regular
            }
            : Fonts.FluentUI.money_hand_20_regular;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
