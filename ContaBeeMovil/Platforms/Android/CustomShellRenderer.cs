using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace ContaBeeMovil
{
    public class CustomShellRenderer : ShellRenderer
    {
        protected override IShellToolbarAppearanceTracker CreateToolbarAppearanceTracker()
        {
            return new CustomToolbarAppearanceTracker(this);
        }
    }

    public class CustomToolbarAppearanceTracker : ShellToolbarAppearanceTracker
    {
        public CustomToolbarAppearanceTracker(IShellContext shellContext) : base(shellContext)
        {
        }

        public override void SetAppearance(
            AndroidX.AppCompat.Widget.Toolbar toolbar,
            IShellToolbarTracker toolbarTracker,
            ShellAppearance appearance)
        {
            base.SetAppearance(toolbar, toolbarTracker, appearance);

            // Siempre leer el tema actual, ignorar appearance.BackgroundColor
            // que puede tener el color cacheado anterior
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var color = isDark ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");
            AplicarColorStatusBar(color);
        }

        public override void ResetAppearance(
            AndroidX.AppCompat.Widget.Toolbar toolbar,
            IShellToolbarTracker toolbarTracker)
        {
            base.ResetAppearance(toolbar, toolbarTracker);

            // Mismo fix: leer tema actual en lugar del color cacheado del Shell
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var color = isDark ? Color.FromArgb("#3a3a3a") : Color.FromArgb("#fefdfc");
            AplicarColorStatusBar(color);
        }

        public static void AplicarColorStatusBar(Color? color)
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity?.Window == null || color == null) return;

            var hex = color.ToArgbHex().TrimStart('#');
            var androidColor = Android.Graphics.Color.ParseColor("#" + hex);

            // Aplicar inmediatamente
            activity.Window.SetStatusBarColor(androidColor);

            // Y también encolar en el looper de Android DESPUÉS de que
            // el Shell termine su propio re-render (Post se ejecuta al final
            // de la cola de mensajes UI actual, no con un timer fijo)
            activity.Window.DecorView.Post(() =>
            {
                activity.Window.SetStatusBarColor(androidColor);

                // Un segundo Post para cubrir cualquier re-render adicional
                activity.Window.DecorView.Post(() =>
                {
                    activity.Window.SetStatusBarColor(androidColor);

                    if (OperatingSystem.IsAndroidVersionAtLeast(23))
                    {
                        var luminance = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
                        var controller = AndroidX.Core.View.WindowCompat
                            .GetInsetsController(activity.Window, activity.Window.DecorView);
                        controller.AppearanceLightStatusBars = luminance > 0.5;
                    }
                });
            });

            // Los iconos también inmediatamente
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                var luminance = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
                var controller = AndroidX.Core.View.WindowCompat
                    .GetInsetsController(activity.Window, activity.Window.DecorView);
                controller.AppearanceLightStatusBars = luminance > 0.5;
            }
        }
    }
}
