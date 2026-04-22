using Contabee.Api.Transcript;
using System.Globalization;

namespace ContaBeeMovil.Converters;

/// <summary>
/// Devuelve el color de fondo del badge central de la card según el estado:
///   ≤4 (pendiente)  → Gris fijo (independiente del tema)
///   5  (finalizado) → Primary (amarillo)
///   6  (error)      → Error (rojo)
/// </summary>
public class EstadoBadgeColorConverter : IValueConverter
{
    // Color gris fijo para pendiente, independiente del tema
    private static readonly Color GrisPendiente = Color.FromRgb(96, 96, 96);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ElementoPaginaCapturaDespliegue item)
            return GrisPendiente;

        return (int)item.Estado switch
        {
            <= 4 => GrisPendiente,
            5    => ResolveColor("Primary"),
            6    => ResolveColor("Error"),
            _    => GrisPendiente
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    internal static Color ResolveColor(string key, Color? fallback = null)
    {
        var app = Application.Current;
        if (app is null) return fallback ?? Colors.Gray;

        if (app.Resources.TryGetValue(key, out object? val))
        {
            if (val is Color c) return c;

            // AppThemeColor del toolkit: probar obtener Light/Dark via reflexión
            var type = val?.GetType();
            if (type is not null)
            {
                var isDark = app.RequestedTheme == AppTheme.Dark;
                var prop = type.GetProperty(isDark ? "Dark" : "Light");
                if (prop?.GetValue(val) is Color themed) return themed;
                prop = type.GetProperty(isDark ? "Light" : "Dark");
                if (prop?.GetValue(val) is Color alt) return alt;
            }
        }
        return fallback ?? Colors.Gray;
    }
}
