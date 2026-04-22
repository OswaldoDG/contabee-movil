using Contabee.Api.Transcript;
using ContaBeeMovil.Helpers;
using System.Globalization;

namespace ContaBeeMovil.Converters;

/// <summary>
/// Fila inferior de la card:
///   ≤4 o 5 → Descripción corta del UsoCfdi
///   6      → Motivo del error
/// </summary>
public class UsoCfdiOErrorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return string.Empty;

        if ((int)item.Estado == 6)
        {
            return item.Motivo switch
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
            };
        }

        var uso = UsoCfdiProvider.GetUsoCfdi()
            .FirstOrDefault(u => u.Codigo == item.ClaveUsoCfdi);

        return uso?.Descripcion ?? item.ClaveUsoCfdi ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
