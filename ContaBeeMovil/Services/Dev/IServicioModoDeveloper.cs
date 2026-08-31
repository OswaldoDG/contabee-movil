namespace ContaBeeMovil.Services.Dev;

public interface IServicioModoDeveloper
{
    void Activar();
    Task<bool> ValidarVigenciaAsync();
}
