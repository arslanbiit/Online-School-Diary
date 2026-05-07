using OnlineSchoolDiary.Shared.Models;

namespace OnlineSchoolDiary.Server.Services;

public sealed class SessionManager
{
    private readonly Dictionary<string, User> _sessions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public string CreateSession(User user)
    {
        var token = Guid.NewGuid().ToString("N");
        lock (_gate) _sessions[token] = user;
        return token;
    }

    public User? GetUser(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        lock (_gate) return _sessions.TryGetValue(token, out var u) ? u : null;
    }
}

