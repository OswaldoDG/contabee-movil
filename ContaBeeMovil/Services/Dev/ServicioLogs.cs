using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Services.Dev;

public class ServicioLogs : IServicioLogs
{
    public void Log(string mensaje)
    {
        var entrada = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
        MainThread.BeginInvokeOnMainThread(() => AppState.Instance.Logs.Add(entrada));
    }

    public void Limpiar()
    {
        MainThread.BeginInvokeOnMainThread(() => AppState.Instance.Logs.Clear());
    }
}
