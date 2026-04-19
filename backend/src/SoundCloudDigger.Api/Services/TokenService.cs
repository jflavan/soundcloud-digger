using System.Collections.Concurrent;

namespace SoundCloudDigger.Api.Services;

public class TokenService : ITokenService
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    private readonly SessionStore? _sessionStore;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();

    /// <summary>
    /// Parameterless constructor — used by legacy tests and in-memory-only scenarios.
    /// The new <see cref="GetValidAccessTokenAsync"/> method requires the full constructor.
    /// </summary>
    public TokenService() { }

    /// <summary>
    /// Full constructor used in production DI.
    /// </summary>
    public TokenService(SessionStore sessionStore, IServiceProvider serviceProvider)
    {
        _sessionStore = sessionStore;
        _serviceProvider = serviceProvider;
    }

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

    public async Task<string?> GetValidAccessTokenAsync(string userUrn)
    {
        if (_sessionStore is null || _serviceProvider is null)
            throw new InvalidOperationException(
                "TokenService must be constructed with SessionStore and IServiceProvider to use GetValidAccessTokenAsync.");

        var semaphore = _refreshLocks.GetOrAdd(userUrn, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var session = _sessionStore.GetActiveSessions()
                .FirstOrDefault(s => s.UserUrn == userUrn);

            if (session is null)
                return null;

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(session.TokenExpiresAt);
            if (expiresAt > DateTimeOffset.UtcNow.AddSeconds(60))
                return session.AccessToken;

            // Token is expired (or within 60-second safety window) — refresh it.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<ISoundCloudClient>();
                var refreshed = await client.RefreshAccessToken(session.RefreshToken);
                var newExpiry = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);
                _sessionStore.UpdateTokens(userUrn, refreshed.AccessToken, refreshed.RefreshToken, newExpiry);
                return refreshed.AccessToken;
            }
            catch
            {
                return null;
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private record TokenEntry(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
