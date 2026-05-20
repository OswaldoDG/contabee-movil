using Serilog;
using Serilog.Context;

namespace ContaBeeMovil.Services.Logging;

public class AppLogger : IAppLogger
{
    private readonly ILogger _logger;

    public AppLogger()
    {
        _logger = Log.ForContext<AppLogger>();
    }

    public void Debug(string eventName, string message, object? data = null)
    {
        using var properties = PushProperties(eventName, data);
        _logger.Debug("{Message}", message);
    }

    public void Info(string eventName, string message, object? data = null)
    {
        using var properties = PushProperties(eventName, data);
        _logger.Information("{Message}", message);
    }

    public void Warning(string eventName, string message, object? data = null)
    {
        using var properties = PushProperties(eventName, data);
        _logger.Warning("{Message}", message);
    }

    public void Error(string eventName, string message, Exception exception, object? data = null)
    {
        using var properties = PushProperties(eventName, data);
        _logger.Error(exception, "{Message}", message);
    }

    public void Fatal(string eventName, string message, Exception exception, object? data = null)
    {
        using var properties = PushProperties(eventName, data);
        _logger.Fatal(exception, "{Message}", message);
    }

    private static IDisposable PushProperties(string eventName, object? data)
    {
        var disposables = new List<IDisposable>
        {
            LogContext.PushProperty("EventName", eventName)
        };

        if (data is Dictionary<string, object?> dictionary)
        {
            foreach (var entry in dictionary)
            {
                disposables.Add(LogContext.PushProperty(entry.Key, entry.Value));
            }
        }
        else if (data is not null)
        {
            disposables.Add(LogContext.PushProperty("Data", data, true));
        }

        return new CompositeDisposable(disposables);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _items;

        public CompositeDisposable(List<IDisposable> items)
        {
            _items = items;
        }

        public void Dispose()
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                _items[i].Dispose();
            }
        }
    }
}
