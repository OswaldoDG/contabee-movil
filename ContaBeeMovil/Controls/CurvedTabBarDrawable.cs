using Microsoft.Maui.Graphics;

namespace ContaBeeMovil.Controls;

/// <summary>
/// Dibuja el fondo de la barra de navegación con una curva convexa hacia ABAJO
/// en el tab activo — el ícono activo "se hunde" en la barra, al estilo
/// curved_navigation_bar de Flutter (modo downward / embedded).
/// </summary>
public class CurvedTabBarDrawable : IDrawable
{
    /// <summary>Desplazamiento vertical desde el borde superior donde comienza la barra.</summary>
    private const float TopOffset = 5f;

    /// <summary>
    /// Posición horizontal normalizada (0.0–1.0) del centro de la curva.
    /// Ejemplo: 0.25 = primer tab de 2, 0.75 = segundo tab de 2.
    /// </summary>
    public float NotchPosition { get; set; } = 0.25f;

    /// <summary>Color de fondo de la barra (debe coincidir con AppThemeResource Primary).</summary>
    public Color BarColor { get; set; } = Helpers.UIHelpers.GetColor("Background");

    /// <summary>Radio de la zona curva (aprox. la mitad del FloatingButton).</summary>
    public float NotchRadius { get; set; } = 30f;

    /// <summary>Profundidad máxima de la curva hacia abajo.</summary>
    public float NotchDepth { get; set; } = 45.5f;

    /// <summary>Margen horizontal extra para suavizar la transición de la curva.</summary>
    public float NotchMargin { get; set; } = 30f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var w = dirtyRect.Width;
        var h = dirtyRect.Height;
        var cx = w * NotchPosition;

        var left = cx - NotchRadius - NotchMargin;
        var right = cx + NotchRadius + NotchMargin;

        var path = new PathF();

        path.MoveTo(0, TopOffset);

        if (left > TopOffset)
            path.LineTo(left, TopOffset);

        path.CurveTo(
            left + NotchMargin, TopOffset,
            cx - NotchRadius, NotchDepth,
            cx, NotchDepth
        );

        path.CurveTo(
            cx + NotchRadius, NotchDepth,
            right - NotchMargin, TopOffset,
            right, TopOffset
        );

        if (right < w)
            path.LineTo(w, TopOffset);

        path.LineTo(w, h);
        path.LineTo(0, h);
        path.Close();

        canvas.FillColor = BarColor;
        canvas.FillPath(path);
    }
}
