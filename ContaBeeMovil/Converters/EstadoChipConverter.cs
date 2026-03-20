using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

/// <summary>
/// Chip inferior de la card. Lógica:
///   0-4  → "Pendiente"
///   5    → FechaFinalizacion formateada, o "Pendiente" si es null
///   6    → texto del motivo de error
/// </summary>
public class EstadoChipConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return string.Empty;

        return (int)item.Estado switch
        {
            <= 4 => "Pendiente",
            5    => item.FechaFinalizacion.HasValue
                        ? item.FechaFinalizacion.Value.ToString("dd/MM/yyyy  h:mm tt", culture)
                        : "Pendiente",
            6    => item.Motivo switch
            {
                MotivoEstado.ImagenDeficiente => "Error: Deficiente",
                MotivoEstado.ImagenErronea => "Error: No es ticket",
                MotivoEstado.Abuso => "Error: Abuso",
                MotivoEstado.MaximoIntentosSuperado => "Error: No localizable",
                MotivoEstado.ReprogramacionFueraRango => "Error: No localizable",
                MotivoEstado.PortalPrivado => "Error: Portal privado",
                MotivoEstado.Extemporaneo => "Error: Error de Fecha",
                MotivoEstado.OtroError => "Error: Otro",
                _ => "Error"
            },
            _ => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
