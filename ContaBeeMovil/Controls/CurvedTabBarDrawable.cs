using Microsoft.Maui.Graphics;

namespace ContaBeeMovil.Controls;

public class CurvedTabBarDrawable : IDrawable
{
    // Reducido de 0.08 a 0.02 → casi sin espacio muerto arriba
    private const float TopOffsetRatio = 0.02f;

    public float NotchPosition { get; set; } = 0.25f;
    public Color BarColor { get; set; } = Helpers.UIHelpers.GetColor("Background");
    public float NotchRadius { get; set; } = 30f;
    public float NotchDepth { get; set; } = 0f;
    public float NotchDepthRatio { get; set; } = 0.75f;
    public float NotchMargin { get; set; } = 30f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var w = dirtyRect.Width;
        var h = dirtyRect.Height;
        var cx = w * NotchPosition;

        float topOffset = h * TopOffsetRatio;
        float notchDepth = NotchDepth > 0 ? NotchDepth : h * NotchDepthRatio;

        var left = cx - NotchRadius - NotchMargin;
        var right = cx + NotchRadius + NotchMargin;

        var path = new PathF();
        path.MoveTo(0, topOffset);

        if (left > topOffset)
            path.LineTo(left, topOffset);

        path.CurveTo(
            left + NotchMargin, topOffset,
            cx - NotchRadius, notchDepth,
            cx, notchDepth);

        path.CurveTo(
            cx + NotchRadius, notchDepth,
            right - NotchMargin, topOffset,
            right, topOffset);

        if (right < w)
            path.LineTo(w, topOffset);

        path.LineTo(w, h);
        path.LineTo(0, h);
        path.Close();

        canvas.FillColor = BarColor;
        canvas.FillPath(path);
    }
}