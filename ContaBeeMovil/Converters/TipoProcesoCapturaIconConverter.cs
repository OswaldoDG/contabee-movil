using Contabee.Api.Transcript;
using MauiIcons.Material;
using System.Globalization;

namespace ContaBeeMovil.Converters;

public class TipoProcesoCapturaIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is TipoProcesoCaptura tipo
            ? tipo switch
            {
                TipoProcesoCaptura.FacturaIndividual => MaterialIcons.Receipt,
                TipoProcesoCaptura.Comprobacion      => MaterialIcons.SwapHoriz,
                TipoProcesoCaptura.Devolucion        => MaterialIcons.Undo,
                _                                    => MaterialIcons.Receipt
            }
            : MaterialIcons.Receipt;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
