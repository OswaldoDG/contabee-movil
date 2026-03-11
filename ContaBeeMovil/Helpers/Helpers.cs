using CommunityToolkit.Maui;

namespace ContaBeeMovil.Helpers;

public static class UIHelpers
{

    public static Color GetColor(string key)

    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            // Caso 1: Es un AppThemeColor del toolkit (tu caso)
            if (value is AppThemeColor themeColor)
            {
                var isDark = Application.Current.RequestedTheme == AppTheme.Dark;
                return isDark ? themeColor.Dark : themeColor.Light;
            }
        }

        return Colors.Transparent;
    }
}
