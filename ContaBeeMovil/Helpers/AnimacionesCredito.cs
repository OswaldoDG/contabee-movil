using Microsoft.Maui.Controls.Shapes;
using SkiaSharp.Extended.UI.Controls;

namespace ContaBeeMovil.Helpers;

/// <summary>
/// Animaciones reutilizables para resaltar una tarjeta de crédito tras una compra.
/// Cada método es autocontenido: guarda el estado original del borde y lo restaura al terminar.
/// </summary>
public static class AnimacionesCredito
{
    // 1. Pulso + glow (sutil) — escala 1.0→1.12→1.0 + flash del borde en su color.
    public static async Task PulsoGlow(Border border, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        border.Stroke = new SolidColorBrush(color);

        await Task.WhenAll(
            border.ScaleToAsync(1.12, 180, Easing.CubicOut),
            TweenGrosor(border, grosorOrig, 3, 180));
        await Task.WhenAll(
            border.ScaleToAsync(1.0, 200, Easing.CubicIn),
            TweenGrosor(border, 3, grosorOrig, 200));

        border.Stroke = strokeOrig;
    }

    // 2. Pop con rebote — crece de golpe y rebota tipo resorte (BounceOut) + glow.
    public static async Task PopRebote(Border border, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        border.Stroke = new SolidColorBrush(color);

        await TweenGrosor(border, grosorOrig, 3, 100);
        await border.ScaleToAsync(1.25, 180, Easing.CubicOut);
        await border.ScaleToAsync(1.0, 380, Easing.BounceOut);
        await TweenGrosor(border, 3, grosorOrig, 160);

        border.Stroke = strokeOrig;
    }

    // 3. Conteo animado — el número sube del valor viejo al nuevo + pop ligero.
    public static async Task ContarHasta(Border border, Label label, int desde, int hasta, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        border.Stroke = new SolidColorBrush(color);

        _ = border.ScaleToAsync(1.10, 160, Easing.CubicOut)
                  .ContinueWith(_ => border.ScaleToAsync(1.0, 220, Easing.CubicIn));

        await ContarLabel(label, desde, hasta, 850);
        await TweenGrosor(border, grosorOrig, 3, 150);
        await TweenGrosor(border, 3, grosorOrig, 200);

        border.Stroke = strokeOrig;
    }

    // 4. "+N" flotante — un badge "+N" sube por encima de la tarjeta y se desvanece + pop.
    public static async Task MasNFlotante(Border border, Label badge, int n, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        border.Stroke = new SolidColorBrush(color);

        badge.Text = $"+{n}";
        badge.TranslationY = 0;
        badge.Opacity = 0;
        badge.IsVisible = true;

        _ = border.ScaleToAsync(1.12, 160, Easing.CubicOut)
                  .ContinueWith(_ => border.ScaleToAsync(1.0, 220, Easing.CubicIn));

        await badge.FadeToAsync(1, 120, Easing.CubicOut);
        await Task.WhenAll(
            badge.TranslateToAsync(0, -38, 720, Easing.CubicOut),
            badge.FadeToAsync(0, 720, Easing.CubicIn),
            TweenGrosor(border, grosorOrig, 3, 200));

        badge.IsVisible = false;
        badge.TranslationY = 0;
        await TweenGrosor(border, 3, grosorOrig, 200);
        border.Stroke = strokeOrig;
    }

    // 6. "+N" flotante seguido del conteo — el badge "+N" aparece y de inmediato
    //    el número sube del valor viejo al nuevo, mientras el "+N" flota y se desvanece.
    public static async Task MasNConConteo(Border border, Label badge, Label numero, int desde, int hasta, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        int n = hasta - desde;

        border.Stroke = new SolidColorBrush(color);
        numero.Text = desde.ToString();

        badge.Text = $"+{n}";
        badge.TranslationY = 0;
        badge.Opacity = 0;
        badge.IsVisible = true;

        // Pop del borde sutil (crece poco) + aparición del "+N".
        _ = border.ScaleToAsync(1.05, 160, Easing.CubicOut)
                  .ContinueWith(_ => border.ScaleToAsync(1.0, 240, Easing.CubicIn));
        await Task.WhenAll(
            badge.FadeToAsync(1, 120, Easing.CubicOut),
            TweenGrosor(border, grosorOrig, 3, 160));

        // Inmediatamente: el "+N" sube y se desvanece mientras el número cuenta.
        await Task.WhenAll(
            badge.TranslateToAsync(0, -40, 760, Easing.CubicOut),
            badge.FadeToAsync(0, 760, Easing.CubicIn),
            ContarLabel(numero, desde, hasta, 760));

        badge.IsVisible = false;
        badge.TranslationY = 0;
        await TweenGrosor(border, 3, grosorOrig, 200);
        border.Stroke = strokeOrig;
    }

    // 5. Shake + glow — la tarjeta vibra de lado a lado un par de veces + flash del borde.
    public static async Task ShakeGlow(Border border, Color color)
    {
        var strokeOrig = border.Stroke;
        var grosorOrig = border.StrokeThickness;
        border.Stroke = new SolidColorBrush(color);

        await TweenGrosor(border, grosorOrig, 3, 80);
        for (int i = 0; i < 2; i++)
        {
            await border.TranslateToAsync(-8, 0, 45, Easing.Linear);
            await border.TranslateToAsync(8, 0, 45, Easing.Linear);
        }
        await border.TranslateToAsync(0, 0, 45, Easing.Linear);
        await TweenGrosor(border, 3, grosorOrig, 140);

        border.Stroke = strokeOrig;
    }

    // ── Monedas (SKConfettiView) ──────────────────────────────────────────────

    private static readonly Color[] DoradosMoneda =
    {
        Color.FromArgb("#FFD700"), // oro
        Color.FromArgb("#F5C518"), // dorado
        Color.FromArgb("#DAA520"), // goldenrod
        Color.FromArgb("#FFC107"), // ámbar
        Color.FromArgb("#E8B923"),
    };

    /// <summary>Sistema de "monedas": estallido dorado desde el centro.</summary>
    public static SKConfettiSystem NuevoEstallidoMonedas()
    {
        var sys = new SKConfettiSystem
        {
            Emitter = SKConfettiEmitter.Burst(90),
            EmitterBounds = SKConfettiEmitterBounds.Center,
            StartAngle = 0,
            EndAngle = 360,
            MinimumInitialVelocity = 250,
            MaximumInitialVelocity = 500,
            Lifetime = 2.5,
            FadeOut = true,
        };

        // Solo círculos dorados (monedas), reemplazando los defaults multicolor.
        sys.Shapes.Clear();
        sys.Shapes.Add(new SKConfettiCircleShape());

        sys.Colors.Clear();
        foreach (var c in DoradosMoneda)
            sys.Colors.Add(c);

        sys.Physics.Clear();
        sys.Physics.Add(new SKConfettiPhysics(16, 1.4)); // monedas grandes
        sys.Physics.Add(new SKConfettiPhysics(12, 1.2)); // y algunas medianas

        return sys;
    }

    // ── Helpers de tween ──────────────────────────────────────────────────────

    private static Task TweenGrosor(Border border, double desde, double hasta, uint duracion)
    {
        var tcs = new TaskCompletionSource();
        new Animation(v => border.StrokeThickness = v, desde, hasta, Easing.CubicInOut)
            .Commit(border, "TweenGrosorCredito", length: duracion, finished: (_, _) => tcs.TrySetResult());
        return tcs.Task;
    }

    private static Task ContarLabel(Label label, int desde, int hasta, uint duracion)
    {
        var tcs = new TaskCompletionSource();
        new Animation(v => label.Text = ((int)Math.Round(v)).ToString(), desde, hasta, Easing.CubicOut)
            .Commit(label, "ContarCredito", length: duracion, finished: (_, _) =>
            {
                label.Text = hasta.ToString();
                tcs.TrySetResult();
            });
        return tcs.Task;
    }
}
