namespace ContaBeeMovil.Services;

public interface IServicioAlerta
{
    Task<bool> MostrarAsync(
        string titulo,
        string mensaje,
        bool verBotonCancelar = true,
        bool verBotonConfirmar = true,
        string cancelarText = "Cancelar",
        string confirmarText = "Si");
}
