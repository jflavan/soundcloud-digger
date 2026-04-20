using Dapper;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Api.Services;

public record SessionRecord(
    string SessionId,
    string UserUrn,
    string AccessToken,
    string RefreshToken,
    long TokenExpiresAt,
    long CreatedAt,
    long LastSeenAt);

public class SessionStore
{
    private readonly SqliteConnection _conn;
    private readonly DbLock _dbLock;

    public SessionStore(SqliteConnection conn, DbLock? dbLock = null)
    {
        _conn = conn;
        _dbLock = dbLock ?? new DbLock();
    }

    public void Create(string sessionId, string userUrn, string accessToken,
        string refreshToken, DateTimeOffset tokenExpires)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var _ = _dbLock.Acquire();
        _conn.Execute(@"
INSERT INTO sessions (session_id, user_urn, access_token, refresh_token,
                     token_expires_at, created_at, last_seen_at)
VALUES (@sessionId, @userUrn, @accessToken, @refreshToken, @expires, @now, @now);",
            new { sessionId, userUrn, accessToken, refreshToken,
                  expires = tokenExpires.ToUnixTimeSeconds(), now });
    }

    public SessionRecord? TryGet(string sessionId)
    {
        using var _ = _dbLock.Acquire();
        return _conn.QueryFirstOrDefault<SessionRecord>(@"
SELECT session_id AS SessionId, user_urn AS UserUrn,
       access_token AS AccessToken, refresh_token AS RefreshToken,
       token_expires_at AS TokenExpiresAt,
       created_at AS CreatedAt, last_seen_at AS LastSeenAt
FROM sessions WHERE session_id=@sessionId;", new { sessionId });
    }

    public void UpdateTokens(string userUrn, string accessToken, string refreshToken,
        DateTimeOffset tokenExpires)
    {
        using var _ = _dbLock.Acquire();
        _conn.Execute(@"
UPDATE sessions SET access_token=@accessToken, refresh_token=@refreshToken,
                    token_expires_at=@expires
WHERE user_urn=@userUrn;",
            new { userUrn, accessToken, refreshToken,
                  expires = tokenExpires.ToUnixTimeSeconds() });
    }

    public void Delete(string sessionId)
    {
        using var _ = _dbLock.Acquire();
        _conn.Execute("DELETE FROM sessions WHERE session_id=@sessionId;",
            new { sessionId });
    }

    public IEnumerable<SessionRecord> GetActiveSessions()
    {
        using var _ = _dbLock.Acquire();
        return _conn.Query<SessionRecord>(@"
SELECT session_id AS SessionId, user_urn AS UserUrn,
       access_token AS AccessToken, refresh_token AS RefreshToken,
       token_expires_at AS TokenExpiresAt,
       created_at AS CreatedAt, last_seen_at AS LastSeenAt
FROM sessions;").ToList();
    }

    public void TouchLastSeen(string sessionId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var _ = _dbLock.Acquire();
        _conn.Execute(
            "UPDATE sessions SET last_seen_at=@now WHERE session_id=@sessionId;",
            new { now, sessionId });
    }
}
