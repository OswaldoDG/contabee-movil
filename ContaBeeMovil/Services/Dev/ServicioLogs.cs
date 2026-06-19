using ContaBeeMovil.Models;
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

    // Límites ajustables
    public static int LimiteEntradasMemoria { get; set; } = 100;
    public static long LimiteBytesArchivo { get; set; } = 5 * 1024 * 1024; // 5 MB

    private static void Registrar(string nivel, string mensaje)
    {
        var entrada = new LogEntrada
        {
            Nivel = nivel,
            Hora = DateTime.Now.ToString("HH:mm:ss"),
            Mensaje = mensaje,
            TextoCompleto = $"[{DateTime.Now:HH:mm:ss}] [{nivel}] {mensaje}"
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var logs = AppState.Instance.Logs;
            if (logs.Count >= LimiteEntradasMemoria)
                logs.RemoveAt(logs.Count - 1);
            logs.Insert(0, entrada);
        });
        _ = EscribirEnArchivoAsync(entrada.TextoCompleto);
    }

    private static async Task EscribirEnArchivoAsync(string linea)
    {
        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "logs");
            Directory.CreateDirectory(dir);
            var archivo = Path.Combine(dir, $"contabee_{DateTime.Now:yyyyMMdd}.log");

            if (File.Exists(archivo) && new FileInfo(archivo).Length >= LimiteBytesArchivo)
            {
                var lineas = await File.ReadAllLinesAsync(archivo);
                var mitad = lineas.Skip(lineas.Length / 2).ToArray();
                await File.WriteAllLinesAsync(archivo, mitad);
            }

            await File.AppendAllTextAsync(archivo, linea + Environment.NewLine);
        }
        catch { }
    }
}
