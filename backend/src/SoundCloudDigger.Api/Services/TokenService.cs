using System.Collections.Concurrent;

namespace SoundCloudDigger.Api.Services;

public class TokenService : ITokenService
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    public void Store(string sessionId, string accessToken, string refreshToken, int expiresIn)
    {
        _tokens[sessionId] = new TokenEntry(accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
    }

    public (string AccessToken, string RefreshToken)? Get(string sessionId)
    {
        if (_tokens.TryGetValue(sessionId, out var entry))
            return (entry.AccessToken, entry.RefreshToken);
        return null;
    }

    public void UpdateTokens(string sessionId, string accessToken, string refreshToken, int expiresIn)
    {
        _tokens[sessionId] = new TokenEntry(accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
    }

    public void Remove(string sessionId)
    {
        _tokens.TryRemove(sessionId, out _);
    }

    public bool IsExpired(string sessionId)
    {
        if (!_tokens.TryGetValue(sessionId, out var entry)) return true;
        return DateTime.UtcNow >= entry.ExpiresAt;
    }

    private record TokenEntry(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
