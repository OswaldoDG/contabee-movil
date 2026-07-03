using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Shapes;

namespace ContaBeeMovil.Helpers;

public static class UIHelpers
{
    private static readonly System.Globalization.CultureInfo _culturaEs = new("es-MX");

    /// <summary>
    /// Convierte un texto (típicamente en MAYÚSCULAS del SAT) a PascalCase/Title Case:
    /// primera letra de cada palabra en mayúscula y el resto en minúscula.
    /// Ej: "JUAN PÉREZ GARCÍA" → "Juan Pérez García".
    /// </summary>
    public static string ToPascalCase(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto ?? string.Empty;
        // ToTitleCase deja intactas las palabras ya en MAYÚSCULAS, por eso se baja a minúsculas primero.
        return _culturaEs.TextInfo.ToTitleCase(texto.ToLower(_culturaEs));
    }

    public static Color GetColor(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            if (value is AppThemeColor themeColor)
            {
                var isDark = Application.Current.RequestedTheme == AppTheme.Dark;
                return isDark ? themeColor.Dark : themeColor.Light;
            }
        }

        return Colors.Transparent;
    }

    public static Border CrearItemSeleccionable(string texto, bool seleccionado, Action alSeleccionar)
    {
        var label = new Label
        {
            Text              = texto,
            FontAttributes    = FontAttributes.Bold,
            FontSize          = 15,
            HorizontalOptions = LayoutOptions.Center,
            TextColor         = seleccionado ? GetColor("OnPrimary") : GetColor("SecondaryText"),
        };

        var borde = new Border
        {
            BackgroundColor = seleccionado ? GetColor("Primary") : GetColor("Alternate"),
            StrokeThickness = 0,
            StrokeShape     = new RoundRectangle { CornerRadius = 12 },
            Padding         = new Thickness(16, 14),
            Content         = label,
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => alSeleccionar();
        borde.GestureRecognizers.Add(tap);

        return borde;
    }
}
