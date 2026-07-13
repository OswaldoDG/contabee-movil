using CommunityToolkit.Maui.Views;

namespace ContaBeeMovil.Views;

/// <summary>
/// Overlay de carga a pantalla completa: scrim con un spinner centrado. No se
/// puede descartar tocando fuera; se cierra por código con <c>CloseAsync</c>
/// cuando termina la operación. Se usa mientras se descarga un archivo antes
/// de abrir el visor o la hoja de compartir.
/// </summary>
public partial class CargandoPopup : Popup
{
    public CargandoPopup() => InitializeComponent();
}
