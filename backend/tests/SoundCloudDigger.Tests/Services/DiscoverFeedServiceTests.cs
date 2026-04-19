using Moq;
using Dapper;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class DiscoverFeedServiceTests
{
    private Microsoft.Data.Sqlite.SqliteConnection CreateDb()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        return conn;
    }

    [Fact]
    public async Task Fetch_SkipsArtistsWithRecentFetch()
    {
        using var conn = CreateDb();
        conn.Execute(@"
INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);
INSERT INTO artist_fetch_state (artist_urn, cursor, last_fetched_at) VALUES ('a1', NULL, @now);",
            new { now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

        var client = new Mock<ISoundCloudClient>();
        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync("at");
        var followings = new Mock<IFollowingsService>();
        followings.Setup(f => f.EnsureAsync("u1")).ReturnsAsync(new[] { "a1" });

        var repo = new DiscoverRepository(conn);
        var svc = new DiscoverFeedService(conn, client.Object, tokens.Object,
            followings.Object, repo);

        await svc.StartFetchAsync("u1");

        client.Verify(c => c.GetUserReposts(It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Fetch_StopsWalkingWhenCursorHit()
    {
        using var conn = CreateDb();
        conn.Execute(
            "INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);");
        conn.Execute(@"
INSERT INTO artist_fetch_state (artist_urn, cursor, last_fetched_at)
VALUES ('a1', 'trackKnown', 0);");

        var client = new Mock<ISoundCloudClient>();
        client.Setup(c => c.GetUserReposts("a1", It.IsAny<string>(), null))
            .ReturnsAsync(new SoundCloudRepostsResponse
            {
                Collection = new()
                {
                    new SoundCloudRepost { CreatedAt = "2026-04-18T00:00:00Z",
                        Track = new SoundCloudTrack { PermalinkUrl = "trackNew", Title = "New" } },
                    new SoundCloudRepost { CreatedAt = "2026-04-17T00:00:00Z",
                        Track = new SoundCloudTrack { PermalinkUrl = "trackKnown", Title = "Known" } },
                    new SoundCloudRepost { CreatedAt = "2026-04-16T00:00:00Z",
                        Track = new SoundCloudTrack { PermalinkUrl = "trackOlder", Title = "Older" } },
                },
                NextHref = "page2",
            });
        // Should NOT be called because walk stopped at trackKnown
        client.Setup(c => c.GetUserReposts("a1", It.IsAny<string>(), "page2"))
            .ThrowsAsync(new InvalidOperationException("should not fetch page 2"));

        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync("at");
        var followings = new Mock<IFollowingsService>();
        followings.Setup(f => f.EnsureAsync("u1")).ReturnsAsync(new[] { "a1" });

        var repo = new DiscoverRepository(conn);
        var svc = new DiscoverFeedService(conn, client.Object, tokens.Object,
            followings.Object, repo);

        await svc.StartFetchAsync("u1");

        Assert.Equal(1L, conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM artist_reposts WHERE track_urn='trackNew';"));
        Assert.Equal(0L, conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM artist_reposts WHERE track_urn='trackOlder';"));
    }

    [Fact]
    public async Task Fetch_IsIdempotentForConcurrentCalls()
    {
        using var conn = CreateDb();
        conn.Execute(
            "INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);");

        var client = new Mock<ISoundCloudClient>();
        var callCount = 0;
        client.Setup(c => c.GetUserReposts("a1", It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(50);
                return new SoundCloudRepostsResponse { Collection = new(), NextHref = null };
            });

        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync("at");
        var followings = new Mock<IFollowingsService>();
        followings.Setup(f => f.EnsureAsync("u1")).ReturnsAsync(new[] { "a1" });

        var repo = new DiscoverRepository(conn);
        var svc = new DiscoverFeedService(conn, client.Object, tokens.Object,
            followings.Object, repo);

        await Task.WhenAll(svc.StartFetchAsync("u1"), svc.StartFetchAsync("u1"));

        Assert.Equal(1, callCount);
    }
}
