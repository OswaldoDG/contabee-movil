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
                MotivoEstado.ImagenDeficiente => "Calidad Deficiente",
                MotivoEstado.ImagenErronea => "No es ticket",
                MotivoEstado.Abuso => "Abuso detectado",
                MotivoEstado.MaximoIntentosSuperado => "Máximo de Intentos superado",
                MotivoEstado.ReprogramacionFueraRango => "Fuera de intervalo",
                MotivoEstado.PortalPrivado => "El portal requiere subscripción",
                MotivoEstado.Extemporaneo => "Fuera de intervalo",
                MotivoEstado.OtroError => $"{item.MensajeError ?? "No pudo procesarse"}",
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
