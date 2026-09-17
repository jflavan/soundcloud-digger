using System.Text.Json;
using Dapper;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Api.Services;

public class FeedCache : IFeedCache
{
    private readonly Db _db;
    private readonly SessionStore _sessionStore;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public FeedCache(Db db, SessionStore sessionStore)
    {
        _db = db;
        _sessionStore = sessionStore;
    }

    public List<FeedTrack> GetTracks(string sessionId)
    {
        var session = _sessionStore.TryGet(sessionId);
        if (session is null) return [];

        using var conn = _db.Open();
        var rows = conn.Query<(string PayloadJson, long AppearedAt)>(@"
SELECT t.payload_json AS PayloadJson, ft.appeared_at AS AppearedAt
FROM feed_tracks ft
JOIN tracks t ON t.urn = ft.track_urn
WHERE ft.user_urn = @userUrn
ORDER BY ft.appeared_at DESC;", new { userUrn = session.UserUrn });

        var result = new List<FeedTrack>();
        foreach (var (payloadJson, _) in rows)
        {
            var track = JsonSerializer.Deserialize<FeedTrack>(payloadJson, Json);
            if (track is not null) result.Add(track);
        }
        return result;
    }

    public void AddTracks(string sessionId, List<FeedTrack> tracks)
    {
        var session = _sessionStore.TryGet(sessionId);
        if (session is null) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var track in tracks)
        {
            var urn = track.PermalinkUrl ?? "";
            var payloadJson = JsonSerializer.Serialize(track, Json);
            var appearedAt = new DateTimeOffset(track.AppearedAt, TimeSpan.Zero).ToUnixTimeSeconds();

            conn.Execute(@"
INSERT INTO tracks (urn, payload_json, updated_at)
VALUES (@urn, @payloadJson, @now)
ON CONFLICT(urn) DO UPDATE SET payload_json = excluded.payload_json, updated_at = excluded.updated_at;",
                new { urn, payloadJson, now }, tx);

            conn.Execute(@"
INSERT OR IGNORE INTO feed_tracks (user_urn, track_urn, appeared_at, activity_type)
VALUES (@userUrn, @urn, @appearedAt, @activityType);",
                new { userUrn = session.UserUrn, urn, appearedAt, activityType = track.ActivityType }, tx);
        }
        tx.Commit();
    }

    public bool IsLoadingComplete(string sessionId)
    {
        var session = _sessionStore.TryGet(sessionId);
        if (session is null) return false;

        using var conn = _db.Open();
        var value = conn.ExecuteScalar<long?>(@"
SELECT feed_last_fetched_at FROM user_fetch_state WHERE user_urn = @userUrn;",
            new { userUrn = session.UserUrn });

        return value is not null;
    }

    public void SetLoadingComplete(string sessionId, bool complete)
    {
        var session = _sessionStore.TryGet(sessionId);
        if (session is null) return;

        var now = complete ? (long?)DateTimeOffset.UtcNow.ToUnixTimeSeconds() : null;

        using var conn = _db.Open();
        conn.Execute(@"
INSERT INTO user_fetch_state (user_urn, feed_last_fetched_at)
VALUES (@userUrn, @now)
ON CONFLICT(user_urn) DO UPDATE SET feed_last_fetched_at = excluded.feed_last_fetched_at;",
            new { userUrn = session.UserUrn, now });
    }

    public void Clear(string sessionId)
    {
        var session = _sessionStore.TryGet(sessionId);
        if (session is null) return;

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute("DELETE FROM feed_tracks WHERE user_urn = @userUrn;",
            new { userUrn = session.UserUrn }, tx);
        conn.Execute(@"
INSERT INTO user_fetch_state (user_urn, feed_last_fetched_at)
VALUES (@userUrn, NULL)
ON CONFLICT(user_urn) DO UPDATE SET feed_last_fetched_at = NULL;",
            new { userUrn = session.UserUrn }, tx);
        tx.Commit();
    }

    public List<string> GetActiveSessionIds()
    {
        return _sessionStore.GetActiveSessions()
            .Select(s => s.SessionId)
            .ToList();
    }
}
