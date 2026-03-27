using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
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
        var popup = new AlertaPopup(titulo, mensaje, verBotonCancelar, verBotonConfirmar, cancelarText, confirmarText);
        await Application.Current!.Windows[0].Page!.ShowPopupAsync(popup);
        return popup.Confirmado;
    }
}
