using System.Reflection;
using System.Text.Json;

namespace ContaBeeMovil.Services.Logging;

public class LogContextService
{
    private readonly string _sessionId;
    private string? _currentUserId;

    public LogContextService()
    {
        _sessionId = Guid.NewGuid().ToString("N");
    }

    public string SessionId => _sessionId;

    public string NewCorrelationId() => Guid.NewGuid().ToString("N");

    public void SetCurrentUserId(string? userId)
    {
        _currentUserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
    }

    public string? ExtractUserIdFromAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

            var bytes = Convert.FromBase64String(payload);
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;

            foreach (var claim in new[] { "sub", "nameid", "uid", "user_id" })
            {
                if (root.TryGetProperty(claim, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public Dictionary<string, object?> BuildCommonContext(string? screen = null, string? correlationId = null, string? userId = null)
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var resolvedUserId = userId ?? _currentUserId;

        return new Dictionary<string, object?>
        {
            ["SessionId"] = _sessionId,
            ["CorrelationId"] = correlationId,
            ["Screen"] = screen,
            ["UserId"] = resolvedUserId,
            ["Device"] = DeviceInfo.Model,
            ["AppVersion"] = appVersion,
            ["Platform"] = DeviceInfo.Platform.ToString(),
            ["OSVersion"] = DeviceInfo.VersionString
        };
    }
}
