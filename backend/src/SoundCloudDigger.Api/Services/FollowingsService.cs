using Dapper;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Api.Services;

public class FollowingsService : IFollowingsService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private readonly SqliteConnection _conn;
    private readonly ISoundCloudClient _client;
    private readonly ITokenService _tokens;
    private readonly DbLock _dbLock;

    public FollowingsService(SqliteConnection conn, ISoundCloudClient client, ITokenService tokens, DbLock? dbLock = null)
    {
        _conn = conn; _client = client; _tokens = tokens;
        _dbLock = dbLock ?? new DbLock();
    }

    public async Task<IReadOnlyList<string>> EnsureAsync(string userUrn)
    {
        if (!IsStale(userUrn)) return LoadCached(userUrn);

        var accessToken = await _tokens.GetValidAccessTokenAsync(userUrn);
        if (accessToken is null) return LoadCached(userUrn);

        string? next = null;
        var all = new List<SoundCloudUser>();
        do
        {
            var page = await _client.GetFollowings(accessToken, next);
            all.AddRange(page.Collection);
            next = page.NextHref;
        } while (next is not null);

        PersistFollowings(userUrn, all);
        return all.Select(u => u.Urn).ToList();
    }

    private bool IsStale(string userUrn)
    {
        using var _ = _dbLock.Acquire();
        var oldest = _conn.ExecuteScalar<long?>(
            "SELECT MIN(fetched_at) FROM followings WHERE user_urn=@u;",
            new { u = userUrn });
        if (oldest is null) return true;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - oldest.Value > (long)Ttl.TotalSeconds;
    }

    private IReadOnlyList<string> LoadCached(string userUrn)
    {
        using var _ = _dbLock.Acquire();
        return _conn.Query<string>(
            "SELECT followed_urn FROM followings WHERE user_urn=@u;",
            new { u = userUrn }).ToList();
    }

    private void PersistFollowings(string userUrn, IEnumerable<SoundCloudUser> users)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var _ = _dbLock.Acquire();
        using var tx = _conn.BeginTransaction();
        _conn.Execute(
            "DELETE FROM followings WHERE user_urn=@u;",
            new { u = userUrn }, tx);
        foreach (var u in users)
        {
            _conn.Execute(@"
INSERT INTO users (urn, username, fetched_at)
VALUES (@urn, @username, @now)
ON CONFLICT(urn) DO UPDATE SET username=excluded.username, fetched_at=excluded.fetched_at;",
                new { urn = u.Urn, username = u.Username, now }, tx);
            _conn.Execute(@"
INSERT OR REPLACE INTO followings (user_urn, followed_urn, fetched_at)
VALUES (@user, @followed, @now);",
                new { user = userUrn, followed = u.Urn, now }, tx);
        }
        tx.Commit();
    }
}
