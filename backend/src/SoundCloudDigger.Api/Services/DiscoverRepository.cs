using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class DiscoverRepository
{
    private readonly SqliteConnection _conn;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DiscoverRepository(SqliteConnection conn) { _conn = conn; }

    private record AggregatedRow(string PayloadJson, long ReposterCount, long LastRepostedAt, string ReposterNames);

    public IReadOnlyList<DiscoverTrack> GetConsensus(string userUrn)
    {
        var rows = _conn.Query<AggregatedRow>(@"
SELECT
  t.payload_json AS PayloadJson,
  COUNT(DISTINCT ar.artist_urn) AS ReposterCount,
  MAX(ar.reposted_at) AS LastRepostedAt,
  GROUP_CONCAT(u.username, '|') AS ReposterNames
FROM artist_reposts ar
JOIN tracks t ON t.urn = ar.track_urn
JOIN users u ON u.urn = ar.artist_urn
WHERE ar.artist_urn IN (SELECT followed_urn FROM followings WHERE user_urn=@u)
GROUP BY t.urn
ORDER BY ReposterCount DESC, LastRepostedAt DESC;",
            new { u = userUrn });

        return rows.Select(r =>
        {
            var track = JsonSerializer.Deserialize<FeedTrack>(r.PayloadJson, Json)!;
            return new DiscoverTrack
            {
                Title = track.Title,
                ArtistName = track.ArtistName,
                ArtworkUrl = track.ArtworkUrl,
                Genre = track.Genre,
                Tags = track.Tags,
                LikesCount = track.LikesCount,
                PlaybackCount = track.PlaybackCount,
                RepostsCount = track.RepostsCount,
                CommentCount = track.CommentCount,
                CreatedAt = track.CreatedAt,
                PermalinkUrl = track.PermalinkUrl,
                Duration = track.Duration,
                Access = track.Access,
                ActivityType = track.ActivityType,
                AppearedAt = track.AppearedAt,
                ReposterCount = (int)r.ReposterCount,
                Reposters = r.ReposterNames.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                           .Distinct().ToArray(),
                LastRepostedAt = DateTimeOffset.FromUnixTimeSeconds(r.LastRepostedAt).UtcDateTime,
            };
        }).ToList();
    }

    public (int fetched, int total) GetProgress(string userUrn)
    {
        var total = (int)_conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM followings WHERE user_urn=@u;", new { u = userUrn });
        if (total == 0) return (0, 0);
        var fetched = (int)_conn.ExecuteScalar<long>(@"
SELECT COUNT(*) FROM artist_fetch_state
WHERE artist_urn IN (SELECT followed_urn FROM followings WHERE user_urn=@u);",
            new { u = userUrn });
        return (fetched, total);
    }

    public string? GetArtistCursor(string artistUrn)
        => _conn.ExecuteScalar<string?>(
            "SELECT cursor FROM artist_fetch_state WHERE artist_urn=@a;",
            new { a = artistUrn });

    public DateTimeOffset? GetArtistLastFetched(string artistUrn)
    {
        var ts = _conn.ExecuteScalar<long?>(
            "SELECT last_fetched_at FROM artist_fetch_state WHERE artist_urn=@a;",
            new { a = artistUrn });
        return ts is null ? null : DateTimeOffset.FromUnixTimeSeconds(ts.Value);
    }

    public DateTimeOffset? GetArtistLastFullReset(string artistUrn)
    {
        var ts = _conn.ExecuteScalar<long?>(
            "SELECT last_full_reset_at FROM artist_fetch_state WHERE artist_urn=@a;",
            new { a = artistUrn });
        return ts is null or 0 ? null : DateTimeOffset.FromUnixTimeSeconds(ts.Value);
    }

    public void UpsertArtistFetchState(string artistUrn, string? cursor, bool didFullReset = false)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resetAt = didFullReset ? now : (long?)null;
        _conn.Execute(@"
INSERT INTO artist_fetch_state (artist_urn, cursor, last_fetched_at, last_full_reset_at)
VALUES (@a, @c, @now, COALESCE(@reset, 0))
ON CONFLICT(artist_urn) DO UPDATE SET
  cursor=excluded.cursor,
  last_fetched_at=excluded.last_fetched_at,
  last_full_reset_at=COALESCE(@reset, artist_fetch_state.last_full_reset_at);",
            new { a = artistUrn, c = cursor, now, reset = resetAt });
    }

    public void DeleteRepostsMissingAfterFullReset(string artistUrn, IEnumerable<string> seenTrackUrns)
    {
        // Caller invokes this only after a full reset walk, having collected every current repost.
        var urns = seenTrackUrns.ToList();
        if (urns.Count == 0)
        {
            _conn.Execute(
                "DELETE FROM artist_reposts WHERE artist_urn=@a;",
                new { a = artistUrn });
            return;
        }

        // Use a temp table instead of `NOT IN @urns` — Dapper expands the latter to one
        // parameter per URN and whale artists can blow past SQLite's variable-count limit.
        using var tx = _conn.BeginTransaction();
        _conn.Execute("CREATE TEMP TABLE IF NOT EXISTS seen_track_urns (urn TEXT PRIMARY KEY);", transaction: tx);
        _conn.Execute("DELETE FROM seen_track_urns;", transaction: tx);
        _conn.Execute(
            "INSERT INTO seen_track_urns (urn) VALUES (@urn);",
            urns.Select(u => new { urn = u }),
            transaction: tx);
        _conn.Execute(@"
DELETE FROM artist_reposts
WHERE artist_urn=@a
  AND track_urn NOT IN (SELECT urn FROM seen_track_urns);",
            new { a = artistUrn }, tx);
        _conn.Execute("DELETE FROM seen_track_urns;", transaction: tx);
        tx.Commit();
    }

    public void UpsertTrackAndRepost(string artistUrn, FeedTrack track, DateTimeOffset repostedAt)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var tx = _conn.BeginTransaction();
        _conn.Execute(@"
INSERT INTO tracks (urn, payload_json, updated_at)
VALUES (@urn, @payload, @now)
ON CONFLICT(urn) DO UPDATE SET payload_json=excluded.payload_json, updated_at=excluded.updated_at;",
            new { urn = track.PermalinkUrl, payload = JsonSerializer.Serialize(track, Json), now }, tx);
        _conn.Execute(@"
INSERT OR IGNORE INTO artist_reposts (artist_urn, track_urn, reposted_at)
VALUES (@a, @t, @ts);",
            new { a = artistUrn, t = track.PermalinkUrl, ts = repostedAt.ToUnixTimeSeconds() }, tx);
        tx.Commit();
    }

    public DateTimeOffset? GetDiscoverLastFetchedAt(string userUrn)
    {
        var ts = _conn.ExecuteScalar<long?>(
            "SELECT discover_last_fetched_at FROM user_fetch_state WHERE user_urn=@u;",
            new { u = userUrn });
        return ts is null ? null : DateTimeOffset.FromUnixTimeSeconds(ts.Value);
    }

    public void MarkDiscoverFetched(string userUrn)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _conn.Execute(@"
INSERT INTO user_fetch_state (user_urn, discover_last_fetched_at)
VALUES (@u, @now)
ON CONFLICT(user_urn) DO UPDATE SET discover_last_fetched_at=excluded.discover_last_fetched_at;",
            new { u = userUrn, now });
    }

    public bool IsLoadingComplete(string userUrn)
    {
        if (GetDiscoverLastFetchedAt(userUrn) is null) return false;
        var (fetched, total) = GetProgress(userUrn);
        return total > 0 && fetched >= total;
    }
}
