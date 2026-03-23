using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

public class TipoProcesoCapturaIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is TipoProcesoCaptura tipo
            ? tipo switch
            {
                TipoProcesoCaptura.FacturaIndividual => Fonts.FluentUI.receipt_20_regular,
                TipoProcesoCaptura.Comprobacion      => Fonts.FluentUI.arrow_swap_20_regular,
                TipoProcesoCaptura.Devolucion        => Fonts.FluentUI.group_return_20_regular,
                _                                    => Fonts.FluentUI.receipt_20_regular
            }
            : Fonts.FluentUI.receipt_20_regular;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
