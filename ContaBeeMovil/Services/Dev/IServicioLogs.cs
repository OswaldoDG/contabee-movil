namespace ContaBeeMovil.Services.Dev;

public interface IServicioLogs
{
    void Log(string mensaje);
    void Info(string mensaje);
    void Warn(string mensaje);
    void Error(string mensaje);
    void Limpiar();
}
