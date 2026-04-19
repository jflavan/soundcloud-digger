using Moq;
using Dapper;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class FollowingsServiceTests
{
    [Fact]
    public async Task Ensure_FetchesFromApiWhenEmpty()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        var tokenSvc = new Mock<ITokenService>();
        tokenSvc.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync("at");

        var client = new Mock<ISoundCloudClient>();
        client.Setup(c => c.GetFollowings("at", null)).ReturnsAsync(
            new SoundCloudUsersResponse
            {
                Collection = new() { new SoundCloudUser { Urn = "soundcloud:users:42", Username = "a" } },
                NextHref = null,
            });

        var svc = new FollowingsService(conn, client.Object, tokenSvc.Object);

        var urns = await svc.EnsureAsync("u1");

        Assert.Contains("soundcloud:users:42", urns);
        Assert.Equal(1, conn.ExecuteScalar<long>("SELECT COUNT(*) FROM followings;"));
    }

    [Fact]
    public async Task Ensure_UsesCacheWithinTtl()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        conn.Execute(@"
INSERT INTO followings (user_urn, followed_urn, fetched_at)
VALUES ('u1', 'soundcloud:users:7', @now);",
            new { now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        conn.Execute(@"
INSERT INTO users (urn, username, fetched_at)
VALUES ('soundcloud:users:7', 'cached', @now);",
            new { now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

        var client = new Mock<ISoundCloudClient>();
        var tokenSvc = new Mock<ITokenService>();

        var svc = new FollowingsService(conn, client.Object, tokenSvc.Object);

        var urns = await svc.EnsureAsync("u1");

        Assert.Single(urns);
        client.Verify(c => c.GetFollowings(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
