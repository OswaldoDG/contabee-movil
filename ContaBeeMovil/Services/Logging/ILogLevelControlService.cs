using Serilog.Events;

namespace ContaBeeMovil.Services.Logging;

public interface ILogLevelControlService
{
    LogEventLevel CurrentLevel { get; }
    void SetMinimumLevel(LogEventLevel level);
    bool IsDebugEnabled();
    void SetDebugEnabled(bool enabled);
}
