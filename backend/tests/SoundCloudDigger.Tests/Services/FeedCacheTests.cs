using System.Text.Json;
using Dapper;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class FeedCacheTests
{
    private (FeedCache cache, Microsoft.Data.Sqlite.SqliteConnection conn, SessionStore store) CreateSut()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        return (new FeedCache(conn, store), conn, store);
    }

    [Fact]
    public void AddTracks_ThenGetTracks_ReturnsPersistedData()
    {
        var (cache, _, _) = CreateSut();
        var track = new FeedTrack
        {
            PermalinkUrl = "https://soundcloud.com/a/one",
            Title = "One", ArtistName = "A", Genre = "Electronic",
            AppearedAt = DateTime.UtcNow, ActivityType = "track",
        };

        cache.AddTracks("s1", new List<FeedTrack> { track });
        var result = cache.GetTracks("s1");

        Assert.Single(result);
        Assert.Equal("One", result[0].Title);
        Assert.Equal("Electronic", result[0].Genre);
    }

    [Fact]
    public void AddTracks_IsIdempotentByPermalinkUrl()
    {
        var (cache, _, _) = CreateSut();
        var track = new FeedTrack
        {
            PermalinkUrl = "https://soundcloud.com/a/one",
            Title = "One", ArtistName = "A",
            AppearedAt = DateTime.UtcNow, ActivityType = "track",
        };

        cache.AddTracks("s1", new List<FeedTrack> { track });
        cache.AddTracks("s1", new List<FeedTrack> { track });

        Assert.Single(cache.GetTracks("s1"));
    }

    [Fact]
    public void GetTracks_ReturnsEmptyForUnknownSession()
    {
        var (cache, _, _) = CreateSut();

        var result = cache.GetTracks("unknown");

        Assert.Empty(result);
    }

    [Fact]
    public void AddTracks_IsNoOpForUnknownSession()
    {
        var (cache, conn, _) = CreateSut();
        var track = new FeedTrack { PermalinkUrl = "u", Title = "T", ArtistName = "A" };

        cache.AddTracks("unknown", new List<FeedTrack> { track });

        Assert.Equal(0L, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM feed_tracks;"));
    }

    [Fact]
    public void SetLoadingComplete_And_IsLoadingComplete_RoundTrip()
    {
        var (cache, _, _) = CreateSut();

        Assert.False(cache.IsLoadingComplete("s1"));
        cache.SetLoadingComplete("s1", true);
        Assert.True(cache.IsLoadingComplete("s1"));
        cache.SetLoadingComplete("s1", false);
        Assert.False(cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public void Clear_RemovesFeedTracksAndResetsLoadingState()
    {
        var (cache, _, _) = CreateSut();
        cache.AddTracks("s1", new List<FeedTrack>
        {
            new FeedTrack { PermalinkUrl = "u1", Title = "T", ArtistName = "A", AppearedAt = DateTime.UtcNow, ActivityType = "track" },
        });
        cache.SetLoadingComplete("s1", true);

        cache.Clear("s1");

        Assert.Empty(cache.GetTracks("s1"));
        Assert.False(cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public void GetActiveSessionIds_ReturnsSessionsFromStore()
    {
        var (cache, _, store) = CreateSut();
        store.Create("s2", "u2", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));

        var ids = cache.GetActiveSessionIds();

        Assert.Contains("s1", ids);
        Assert.Contains("s2", ids);
    }

    [Fact]
    public void Tracks_SharedAcrossSessionsOfSameUser()
    {
        var (cache, _, store) = CreateSut();
        store.Create("s2", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        cache.AddTracks("s1", new List<FeedTrack>
        {
            new FeedTrack { PermalinkUrl = "u", Title = "T", ArtistName = "A", AppearedAt = DateTime.UtcNow, ActivityType = "track" },
        });

        var viaS2 = cache.GetTracks("s2");

        Assert.Single(viaS2);
    }
}
