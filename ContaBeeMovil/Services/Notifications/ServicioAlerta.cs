using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using ContaBeeMovil.Pages.SinConexion;
using ContaBeeMovil.Views;

namespace ContaBeeMovil.Services.Notifications;

public class ServicioAlerta : IServicioAlerta
{
    public async Task<bool> MostrarAsync(
        string titulo,
        string mensaje,
        bool verBotonCancelar = true,
        bool verBotonConfirmar = true,
        string cancelarText = "Cancelar",
        string confirmarText = "Si")
    {
        // Suprimir alertas cuando la pantalla "Sin conexión" ya está activa
        if (Application.Current?.Windows[0].Page is PaginaSinConexion)
            return false;

        var popup = new AlertaPopup(titulo, mensaje, verBotonCancelar, verBotonConfirmar, cancelarText, confirmarText);
        await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);
        return popup.Confirmado;
    }
}
