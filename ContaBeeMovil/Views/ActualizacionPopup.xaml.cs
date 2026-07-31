using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

public partial class ActualizacionPopup : Popup
{
    /// <summary>true si el usuario tocó "Actualizar".</summary>
    public bool Confirmado { get; private set; }

    /// <summary>
    /// true solo si tocó "Ahora no" explícitamente. Cerrar tocando fuera del popup
    /// deja ambas en false: se distingue para no silenciar el aviso por días
    /// a causa de un tap accidental.
    /// </summary>
    public bool Pospuesto { get; private set; }

    /// <param name="mensaje">Texto configurable del backend; si viene vacío se usa el default.</param>
    /// <param name="versionInstalada">Versión de la app en el dispositivo. Si viene vacía se oculta la píldora.</param>
    /// <param name="versionNueva">Versión vigente según el backend. Si viene vacía se oculta la píldora.</param>
    public ActualizacionPopup(string? mensaje, string? versionInstalada, string? versionNueva)
    {
        InitializeComponent();

        var anchoPantalla = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        ContenedorPopup.MinimumWidthRequest = anchoPantalla * 0.70;
        ContenedorPopup.WidthRequest = Math.Min(anchoPantalla * 0.86, 420);

        LblMensaje.Text = !string.IsNullOrWhiteSpace(mensaje)
            ? mensaje
            : "Hay una nueva versión de ContaBee con mejoras y correcciones.";

        // La píldora solo tiene sentido con ambas versiones; el fallback a tienda
        // o un backend sin configurar pueden dejar alguna vacía.
        var hayVersiones = !string.IsNullOrWhiteSpace(versionInstalada)
                           && !string.IsNullOrWhiteSpace(versionNueva);

        PildoraVersiones.IsVisible = hayVersiones;
        if (hayVersiones)
        {
            LblVersionInstalada.Text = versionInstalada;
            LblVersionNueva.Text = versionNueva;
        }
    }

    private void OnActualizar(object? sender, EventArgs e)
    {
        Confirmado = true;
        _ = CloseAsync();
    }

    private void OnAhoraNo(object? sender, TappedEventArgs e)
    {
        Pospuesto = true;
        _ = CloseAsync();
    }
}
