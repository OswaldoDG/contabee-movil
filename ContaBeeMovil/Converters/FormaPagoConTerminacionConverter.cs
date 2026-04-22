using System.Globalization;
using Contabee.Api.Transcript;
using ContaBeeMovil.Helpers;

namespace ContaBeeMovil.Converters;

public class FormaPagoConTerminacionConverter : IMultiValueConverter
{
    public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return string.Empty;

        var claveFormaPago = values[0]?.ToString();
        var terminacion = values[1]?.ToString();

        if (string.IsNullOrWhiteSpace(claveFormaPago))
            return string.Empty;

        // Buscar la forma de pago en el provider
        var formaPago = FormaPagoProvider.GetFormasPago()
            .FirstOrDefault(fp => fp.Codigo == claveFormaPago);

        if (formaPago == null)
            return string.Empty;

        // Para tarjetas de crédito (4) o débito (28), mostrar abreviatura con terminación
        if (claveFormaPago == "4")
        {
            // Tarjeta de crédito
            if (!string.IsNullOrWhiteSpace(terminacion))
                return $"TDC {terminacion}";
            return "TDC";
        }
        else if (claveFormaPago == "28")
        {
            // Tarjeta de débito
            if (!string.IsNullOrWhiteSpace(terminacion))
                return $"TDD {terminacion}";
            return "TDD";
        }

        // Para otras formas de pago, mostrar la descripción
        return formaPago.Descripcion;
    }

    public object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
