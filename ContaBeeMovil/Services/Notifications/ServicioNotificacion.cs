using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Font = Microsoft.Maui.Font;
using ContaBeeMovil.Helpers;
using CommunityToolkit.Maui.Extensions;



#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace ContaBeeMovil.Services.Notifications;

public class ServicioNotificacion : IServicioNotificacion
{
    public Task MostrarErrorAsync(string mensaje)
    {
        var (isDark, app) = ObtenerTema();
        if (app == null) return Task.CompletedTask;

        // Error: page background, dark text, golden border
        //var fondo = ResolverColor(app, isDark, "Background", "SecondaryBackground");
        //var texto = ResolverColor(app, isDark, "PrimaryText", "PrimaryText");
        //var borde = ResolverColor(app, isDark, "Primary", "Primary");
        Color fondo = UIHelpers.GetColor("Background");  
        Color texto = UIHelpers.GetColor("PrimaryText");
        Color borde = UIHelpers.GetColor("Primary");

        return MostrarSnackbarConBordeAsync(mensaje, fondo, texto, borde);
    }

    public Task MostrarExitoAsync(string mensaje)
    {
        var (isDark, app) = ObtenerTema();
        if (app == null) return Task.CompletedTask;

        var fondo = ResolverColor(app, isDark, "Success", "Success");
        var texto = ResolverColor(app, isDark, "Background", "PrimaryText");

        return MostrarSnackbarAsync(mensaje, fondo, texto);
    }

    public Task MostrarInfoAsync(string mensaje)
    {
        var (isDark, app) = ObtenerTema();
        if (app == null) return Task.CompletedTask;

        var fondo = ResolverColor(app, isDark, "accent1", "Accent1");
        var texto = ResolverColor(app, isDark, "Background", "PrimaryText");

        return MostrarSnackbarAsync(mensaje, fondo, texto);
    }

    public async Task ShowAlert(string mensaje)
    {
        var popup = new CustomToast(mensaje);
        // PageExtensions.ShowPopup es un método del Toolkit
        await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);
    }

    private static (bool isDark, Application? app) ObtenerTema()
    {
        var app = Application.Current;
        if (app == null) return (false, null);

        var isDark = app.UserAppTheme == AppTheme.Dark ||
                     (app.UserAppTheme == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark);
        return (isDark, app);
    }

    private static Color ResolverColor(Application app, bool isDark, string claveLight, string claveDark)
    {
        var clave = isDark ? claveDark : claveLight;
        return app.Resources.TryGetValue(clave, out var obj) && obj is Color color
            ? color
            : Colors.Gray;
    }

    /// <summary>
    /// Error snackbar using native Android Snackbar with golden border + close button.
    /// Native Snackbar handles positioning and touch events reliably on Android.
    /// </summary>
    private static Task MostrarSnackbarConBordeAsync(
        string mensaje, Color fondo, Color texto, Color borde)
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.FindViewById(Android.Resource.Id.Content) is not Android.Views.View rootView)
            return Task.CompletedTask;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var snackbar = Google.Android.Material.Snackbar.Snackbar.Make(
                rootView, mensaje, 4000);

            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;

            // Convert MAUI colors to Android ColorStateLists
            var fondoInt = (int)fondo.ToPlatform();
            var textoColorState = Android.Content.Res.ColorStateList.ValueOf(
                new Android.Graphics.Color((int)texto.ToPlatform()));
            var bordeColorState = Android.Content.Res.ColorStateList.ValueOf(
                new Android.Graphics.Color((int)borde.ToPlatform()));

            // Background drawable with golden border
            var bg = new Android.Graphics.Drawables.GradientDrawable();
            bg.SetColor(fondoInt);
            bg.SetStroke((int)(2 * density), bordeColorState);
            bg.SetCornerRadius(12 * density);
            snackbar.View.Background = bg;
            snackbar.View.Elevation = 8 * density;

            // Margins
            if (snackbar.View.LayoutParameters is Android.Views.ViewGroup.MarginLayoutParams mp)
            {
                int m16 = (int)(16 * density);
                mp.LeftMargin = m16;
                mp.RightMargin = m16;
                mp.BottomMargin = (int)(24 * density);
            }

            // Style text color
            snackbar.SetTextColor(textoColorState);

            // Close action with golden color
            snackbar.SetAction("\u2715", _ => snackbar.Dismiss());
            snackbar.SetActionTextColor(bordeColorState);

            snackbar.Show();
        });

        return Task.CompletedTask;
#else
        return Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Standard snackbar for success and info notifications.
    /// </summary>
    private static async Task MostrarSnackbarAsync(string mensaje, Color fondo, Color texto)
    {
        var opciones = new SnackbarOptions
        {
            BackgroundColor = fondo,
            TextColor = texto,
            CornerRadius = new CornerRadius(12),
            Font = Font.SystemFontOfSize(14),
        };

        var snackbar = Snackbar.Make(mensaje, visualOptions: opciones, duration: TimeSpan.FromSeconds(4));
        await snackbar.Show();
    }
}
