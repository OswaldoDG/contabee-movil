namespace ContaBeeMovil.Models;

public class LogEntrada
{
    public string Nivel { get; init; } = "INFO";
    public string Hora { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;
    public string TextoCompleto { get; init; } = string.Empty;

    public Color ColorAccent => Nivel switch
    {
        "ERROR" => Color.FromArgb("#F44336"),
        "WARN"  => Color.FromArgb("#FF9800"),
        _       => Color.FromArgb("#4CAF50")
    };

    public Color ColorFondo => Nivel switch
    {
        "ERROR" => Color.FromArgb("#1AF44336"),
        "WARN"  => Color.FromArgb("#1AFF9800"),
        _       => Color.FromArgb("#1A4CAF50")
    };
}
