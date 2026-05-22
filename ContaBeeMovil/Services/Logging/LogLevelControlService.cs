using Serilog.Core;
using Serilog.Events;

namespace ContaBeeMovil.Services.Logging;

public class LogLevelControlService : ILogLevelControlService
{
    private const string DebugLoggingEnabledKey = "DebugLoggingEnabled";
    private readonly LoggingLevelSwitch _levelSwitch;

    public LogLevelControlService(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch;
    }

    public LogEventLevel CurrentLevel => _levelSwitch.MinimumLevel;

    public void SetMinimumLevel(LogEventLevel level)
    {
        _levelSwitch.MinimumLevel = level;
    }

    public bool IsDebugEnabled()
    {
        return Preferences.Get(DebugLoggingEnabledKey, true);
    }

    public void SetDebugEnabled(bool enabled)
    {
        Preferences.Set(DebugLoggingEnabledKey, enabled);
        SetMinimumLevel(enabled ? LogEventLevel.Debug : LogEventLevel.Information);
    }
}
