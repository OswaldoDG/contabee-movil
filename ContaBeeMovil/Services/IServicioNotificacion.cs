namespace ContaBeeMovil.Services;

public interface IServicioNotificacion
{
    Task MostrarErrorAsync(string mensaje);
    Task MostrarExitoAsync(string mensaje);
    Task MostrarInfoAsync(string mensaje);
}
