using Serilog;
using Serilog.Context;
using System.Collections;
using System.Reflection;

namespace Contabee.Api.Logging;

public class ApiAppLogger : IAppLogger
{
    private readonly ILogger _logger;
    private readonly string _sessionId;
    private string? _sessionUserId;

    private static readonly HashSet<string> ReservedContextKeys =
    [
        "EventName", "SessionId", "CorrelationId", "Screen", "UserId", "Device", "AppVersion", "Platform", "OSVersion"
    ];

    public ApiAppLogger()
    {
        _logger = Log.ForContext<ApiAppLogger>();
        _sessionId = Guid.NewGuid().ToString("N");
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

    private IDisposable PushProperties(string eventName, object? data)
    {
        var correlationId = ExtraerValor(data, "CorrelationId", "correlationId") ?? Guid.NewGuid().ToString("N");
        var screen = ExtraerValor(data, "Screen", "screen") ?? ObtenerScreenActual();
        var userId = ObtenerUserId(data);
        var device = ObtenerDevice();
        var appVersion = ObtenerAppVersion();
        var platform = ObtenerPlatform();
        var osVersion = ObtenerOsVersion();

        var disposables = new List<IDisposable>
        {
            LogContext.PushProperty("EventName", eventName),
            LogContext.PushProperty("SessionId", _sessionId),
            LogContext.PushProperty("CorrelationId", correlationId),
            LogContext.PushProperty("Screen", screen),
            LogContext.PushProperty("UserId", userId),
            LogContext.PushProperty("Device", device),
            LogContext.PushProperty("AppVersion", appVersion),
            LogContext.PushProperty("Platform", platform),
            LogContext.PushProperty("OSVersion", osVersion)
        };

        if (data is Dictionary<string, object?> dictionary)
        {
            foreach (var entry in dictionary)
            {
                if (ReservedContextKeys.Contains(entry.Key))
                    continue;

                disposables.Add(LogContext.PushProperty(entry.Key, entry.Value));
            }
        }
        else if (data is not null)
        {
            disposables.Add(LogContext.PushProperty("Data", data, true));
        }

        return new CompositeDisposable(disposables);
    }

    private string ObtenerUserId(object? data)
    {
        var fromData = ExtraerValor(data, "UserId", "userId", "UsuarioId", "usuarioId");
        if (!string.IsNullOrWhiteSpace(fromData))
        {
            _sessionUserId = fromData;
            return fromData!;
        }

        if (!string.IsNullOrWhiteSpace(_sessionUserId))
            return _sessionUserId!;

        var fromPreferenceUserId = ObtenerPreference("LogUserId") ?? ObtenerSecureStorage("LogUserId");
        if (!string.IsNullOrWhiteSpace(fromPreferenceUserId))
        {
            _sessionUserId = fromPreferenceUserId;
            return fromPreferenceUserId!;
        }

        var fromAppState = ObtenerUserIdDesdeAppState();
        if (!string.IsNullOrWhiteSpace(fromAppState))
        {
            _sessionUserId = fromAppState;
            return fromAppState!;
        }

        var fromPreferences = ObtenerPreference("CredencialEmail") ?? ObtenerSecureStorage("CredencialEmail");
        if (!string.IsNullOrWhiteSpace(fromPreferences))
            return fromPreferences!;

        return "-";
    }

    private static string? ExtraerValor(object? data, params string[] keys)
    {
        if (data is Dictionary<string, object?> dictionary)
        {
            foreach (var key in keys)
            {
                if (dictionary.TryGetValue(key, out var value))
                    return value?.ToString();
            }
        }

        return null;
    }

    private static string ObtenerDevice()
        => ObtenerMauiDeviceInfo("Model")
           ?? Environment.MachineName
           ?? "-";

    private static string ObtenerPlatform()
        => ObtenerMauiDeviceInfo("Platform")
           ?? System.Runtime.InteropServices.RuntimeInformation.OSDescription
           ?? "-";

    private static string ObtenerOsVersion()
        => ObtenerMauiDeviceInfo("VersionString")
           ?? Environment.OSVersion.VersionString
           ?? "-";

    private static string ObtenerAppVersion()
    {
        var appInfoType = ObtenerTipo("Microsoft.Maui.ApplicationModel.AppInfo");
        var current = appInfoType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        var version = current?.GetType().GetProperty("VersionString")?.GetValue(current)?.ToString();
        if (!string.IsNullOrWhiteSpace(version))
            return version;

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
               ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
               ?? "-";
    }

    private static string ObtenerScreenActual()
    {
        try
        {
            var appType = ObtenerTipo("Microsoft.Maui.Controls.Application");
            var currentApp = appType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (currentApp is null) return "-";

            var windows = currentApp.GetType().GetProperty("Windows")?.GetValue(currentApp) as IEnumerable;
            var firstWindow = windows?.Cast<object>().FirstOrDefault();
            var page = firstWindow?.GetType().GetProperty("Page")?.GetValue(firstWindow);
            if (page is null) return "-";

            page = page.GetType().GetProperty("CurrentPage")?.GetValue(page) ?? page;
            page = page.GetType().GetProperty("CurrentPage")?.GetValue(page) ?? page;

            return page.GetType().Name;
        }
        catch
        {
            return "-";
        }
    }

    private static string? ObtenerUserIdDesdeAppState()
    {
        try
        {
            var appStateType = ObtenerTipo("ContaBeeMovil.Services.Device.AppState");
            var instance = appStateType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance is null)
                return null;

            var email = ObtenerPreference("CredencialEmail") ?? ObtenerSecureStorage("CredencialEmail");
            var misUsuarios = appStateType?.GetProperty("MisUsuarios")?.GetValue(instance) as IEnumerable;

            if (misUsuarios is not null)
            {
                foreach (var usuario in misUsuarios.Cast<object>())
                {
                    var userEmail = usuario.GetType().GetProperty("Email")?.GetValue(usuario)?.ToString();
                    var userName = usuario.GetType().GetProperty("UserName")?.GetValue(usuario)?.ToString();
                    var id = usuario.GetType().GetProperty("Id")?.GetValue(usuario)?.ToString();

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (!string.IsNullOrWhiteSpace(email)
                        && (string.Equals(userEmail, email, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(userName, email, StringComparison.OrdinalIgnoreCase)))
                    {
                        return id;
                    }
                }

                var primero = misUsuarios.Cast<object>().FirstOrDefault();
                var idPrimero = primero?.GetType().GetProperty("Id")?.GetValue(primero)?.ToString();
                if (!string.IsNullOrWhiteSpace(idPrimero))
                    return idPrimero;
            }

            var perfil = appStateType?.GetProperty("Perfil")?.GetValue(instance);
            var perfilId = perfil?.GetType().GetProperty("Id")?.GetValue(perfil)?.ToString();
            return string.IsNullOrWhiteSpace(perfilId) ? null : perfilId;
        }
        catch
        {
            return null;
        }
    }

    private static string? ObtenerMauiDeviceInfo(string propertyName)
    {
        var deviceInfoType = ObtenerTipo("Microsoft.Maui.Devices.DeviceInfo");
        var prop = deviceInfoType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        return prop?.GetValue(null)?.ToString();
    }

    private static string? ObtenerPreference(string key)
    {
        try
        {
            var preferencesType = ObtenerTipo("Microsoft.Maui.Storage.Preferences");
            var defaultObj = preferencesType?.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (defaultObj is null)
                return null;

            var getMethod = defaultObj.GetType().GetMethod("Get", [typeof(string), typeof(string)]);
            return getMethod?.Invoke(defaultObj, [key, string.Empty])?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ObtenerSecureStorage(string key)
    {
        try
        {
            var secureStorageType = ObtenerTipo("Microsoft.Maui.Storage.SecureStorage");
            var defaultObj = secureStorageType?.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (defaultObj is null)
                return null;

            var getAsync = defaultObj.GetType().GetMethod("GetAsync", [typeof(string)]);
            var taskObj = getAsync?.Invoke(defaultObj, [key]);
            if (taskObj is not Task<string?> task)
                return null;

            return task.ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private static Type? ObtenerTipo(string fullName)
    {
        var type = Type.GetType(fullName);
        if (type is not null)
            return type;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type is not null)
                return type;
        }

        return null;
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
