using System.Text;
using System.Text.Json;

namespace SkillSnap.Client.Services;

public class UserSessionService
{
    public string? UserId { get; private set; }

    public string? Role { get; private set; }

    public int? CurrentEditingProjectId { get; private set; }

    public string CurrentEditingProjectTitle { get; private set; } = string.Empty;

    public string CurrentEditingProjectDescription { get; private set; } = string.Empty;

    public event Action? SessionChanged;

    public void SetUserContext(string? userId, string? role)
    {
        UserId = userId;
        Role = role;
        SessionChanged?.Invoke();
    }

    public void SetUserContextFromJwt(string token)
    {
        var userId = ReadClaim(token, "nameid") ?? ReadClaim(token, "sub");
        var role = ReadClaim(token, "role")
            ?? ReadClaim(token, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

        SetUserContext(userId, role);
    }

    public void SetEditingProjectState(int? projectId, string? title, string? description)
    {
        CurrentEditingProjectId = projectId;
        CurrentEditingProjectTitle = title ?? string.Empty;
        CurrentEditingProjectDescription = description ?? string.Empty;
        SessionChanged?.Invoke();
    }

    public void ClearEditingProjectState()
    {
        CurrentEditingProjectId = null;
        CurrentEditingProjectTitle = string.Empty;
        CurrentEditingProjectDescription = string.Empty;
        SessionChanged?.Invoke();
    }

    public void ClearAll()
    {
        UserId = null;
        Role = null;
        ClearEditingProjectState();
    }

    private static string? ReadClaim(string token, string claimName)
    {
        var payload = token.Split('.');
        if (payload.Length < 2)
        {
            return null;
        }

        try
        {
            var json = DecodeBase64Url(payload[1]);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(claimName, out var value))
            {
                if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
                {
                    return value[0].GetString();
                }

                return value.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');

        // JWT payloads can omit '=' padding for URL-safe encoding.
        var remainder = base64.Length % 4;
        if (remainder > 0)
        {
            base64 = base64.PadRight(base64.Length + (4 - remainder), '=');
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
}
