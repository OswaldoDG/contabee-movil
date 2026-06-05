using ContaBeeMovil.Services.Device;

namespace ContaBeeMovil.Services.Dev;

public class ServicioLogs : IServicioLogs
{
    public void Log(string mensaje) => Info(mensaje);
    public void Info(string mensaje) => Registrar("INFO", mensaje);
    public void Warn(string mensaje) => Registrar("WARN", mensaje);
    public void Error(string mensaje) => Registrar("ERROR", mensaje);

    public void Limpiar()
    {
        MainThread.BeginInvokeOnMainThread(() => AppState.Instance.Logs.Clear());
    }

    private static void Registrar(string nivel, string mensaje)
    {
        var entrada = $"[{DateTime.Now:HH:mm:ss}] [{nivel}] {mensaje}";
        MainThread.BeginInvokeOnMainThread(() => AppState.Instance.Logs.Add(entrada));
        _ = EscribirEnArchivoAsync(entrada);
    }

    private static async Task EscribirEnArchivoAsync(string linea)
    {
        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "logs");
            Directory.CreateDirectory(dir);
            var archivo = Path.Combine(dir, $"contabee_{DateTime.Now:yyyyMMdd}.log");
            await File.AppendAllTextAsync(archivo, linea + Environment.NewLine);
        }
        catch { }
    }
}
