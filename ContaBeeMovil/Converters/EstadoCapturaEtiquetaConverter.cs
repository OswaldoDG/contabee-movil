using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

public class EstadoCapturaEtiquetaConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return string.Empty;

        return (int)item.Estado switch
        {
            <= 4 => "Pendiente",
            5    => "Finalizado",
            6    => item.Motivo switch
            {
                MotivoEstado.ImagenDeficiente        => "Error: Deficiente",
                MotivoEstado.ImagenErronea           => "Error: No es ticket",
                MotivoEstado.Abuso                   => "Error: Abuso",
                MotivoEstado.MaximoIntentosSuperado  => "Error: No localizable",
                MotivoEstado.ReprogramacionFueraRango => "Error: No localizable",
                MotivoEstado.PortalPrivado           => "Error: Portal privado",
                MotivoEstado.Extemporaneo            => "Error: Error de Fecha",
                MotivoEstado.OtroError               => "Error: Otro",
                _                                    => "Error"
            },
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
