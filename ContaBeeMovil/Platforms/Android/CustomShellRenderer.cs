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

            // Ignorar appearance.BackgroundColor (puede tener color cacheado anterior).
            // Siempre usar AppBarPrimary según el tema actual.
            AplicarColorStatusBar(AppShell.ObtenerColorStatusBar());
        }

        public override void ResetAppearance(
            AndroidX.AppCompat.Widget.Toolbar toolbar,
            IShellToolbarTracker toolbarTracker)
        {
            base.ResetAppearance(toolbar, toolbarTracker);

            // Mismo fix: forzar AppBarPrimary en lugar del color cacheado del Shell.
            AplicarColorStatusBar(AppShell.ObtenerColorStatusBar());
        }

        public static void AplicarColorStatusBar(Color? color)
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity?.Window == null || color == null) return;

            var androidColor = Android.Graphics.Color.Argb(
                (int)(color.Alpha * 255),
                (int)(color.Red * 255),
                (int)(color.Green * 255),
                (int)(color.Blue * 255));

            // Aplicar inmediatamente
            activity.Window.SetStatusBarColor(androidColor);

            // Post en el looper de Android DESPUÉS de que el Shell termine su propio
            // re-render (Post se ejecuta al final de la cola de mensajes UI actual)
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

            // Iconos también inmediatamente
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
