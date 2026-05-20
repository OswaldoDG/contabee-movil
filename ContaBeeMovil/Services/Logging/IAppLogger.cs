namespace ContaBeeMovil.Services.Logging;

public interface IAppLogger
{
    void Debug(string eventName, string message, object? data = null);
    void Info(string eventName, string message, object? data = null);
    void Warning(string eventName, string message, object? data = null);
    void Error(string eventName, string message, Exception exception, object? data = null);
    void Fatal(string eventName, string message, Exception exception, object? data = null);
}
