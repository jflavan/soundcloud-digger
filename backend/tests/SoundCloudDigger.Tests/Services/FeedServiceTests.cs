using Microsoft.Data.Sqlite;
using Moq;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class FeedServiceTests : IDisposable
{
    private readonly Mock<ISoundCloudClient> _mockClient = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly SqliteConnection _db;
    private readonly FeedCache _cache;
    private readonly FeedService _sut;

    public FeedServiceTests()
    {
        _db = Db.OpenInMemory();
        SchemaMigrator.Migrate(_db, new IMigration[] { new V1_InitialSchema() });
        var store = new SessionStore(_db);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        _cache = new FeedCache(_db, store);
        _sut = new FeedService(_mockClient.Object, _cache, _mockTokenService.Object);
    }

    public void Dispose() => _db.Dispose();

    private static SoundCloudActivitiesResponse MakeResponse(
        List<(string title, DateTime createdAt, int likes)> tracks,
        string? nextHref = null)
    {
        return new SoundCloudActivitiesResponse
        {
            Collection = tracks.Select(t => new SoundCloudActivity
            {
                Type = "track",
                CreatedAt = t.createdAt,
                Origin = new SoundCloudTrack
                {
                    Title = t.title,
                    CreatedAt = t.createdAt,
                    FavoritingsCount = t.likes,
                    User = new SoundCloudUser { Username = "artist" },
                    PermalinkUrl = $"https://soundcloud.com/artist/{t.title.ToLower().Replace(" ", "-")}",
                },
            }).ToList(),
            NextHref = nextHref,
        };
    }

    [Fact]
    public async Task StartFetch_FetchesSinglePage_MarksComplete()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("Track A", now, 100)]));

        await _sut.StartFetchAsync("s1");

        Assert.True(_cache.IsLoadingComplete("s1"));
        Assert.Single(_cache.GetTracks("s1"));
    }

    [Fact]
    public async Task StartFetch_PaginatesThroughMultiplePages()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("A", now, 10)], "https://next-page"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, "https://next-page"))
            .ReturnsAsync(MakeResponse([("B", now, 20)]));

        await _sut.StartFetchAsync("s1");

        Assert.Equal(2, _cache.GetTracks("s1").Count);
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_StopsWhenAllTracksOlderThan24Hours()
    {
        var old = DateTime.UtcNow.AddDays(-2);
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("Old Track", old, 5)], "https://next-page"));

        await _sut.StartFetchAsync("s1");

        _mockClient.Verify(c => c.GetFeedTracks("token", 200, "https://next-page"), Times.Never);
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_BacksOffOn429()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));

        var callCount = 0;
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
                return MakeResponse([("Track A", now, 100)]);
            });

        await _sut.StartFetchAsync("s1");

        Assert.Single(_cache.GetTracks("s1"));
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_NoToken_DoesNotFetch()
    {
        _mockTokenService.Setup(t => t.Get("s1")).Returns((ValueTuple<string, string>?)null);

        await _sut.StartFetchAsync("s1");

        Assert.Empty(_cache.GetTracks("s1"));
        _mockClient.Verify(c => c.GetFeedTracks(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task StartFetch_On401_RefreshesTokenAndRetries()
    {
        var now = DateTime.UtcNow;

        _mockTokenService.SetupSequence(t => t.IsExpired("s1"))
            .Returns(false)
            .Returns(true)
            .Returns(true);

        _mockTokenService.SetupSequence(t => t.Get("s1"))
            .Returns(("token", "refresh"))
            .Returns(("token", "refresh"))
            .Returns(("new_token", "new_refresh"));

        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        _mockClient.Setup(c => c.GetFeedTracks("new_token", 200, null))
            .ReturnsAsync(MakeResponse([("Track A", now, 100)]));

        _mockClient.Setup(c => c.RefreshAccessToken("refresh"))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = "new_token",
                RefreshToken = "new_refresh",
                ExpiresIn = 3600,
            });

        await _sut.StartFetchAsync("s1");

        Assert.Single(_cache.GetTracks("s1"));
        _mockTokenService.Verify(t => t.UpdateTokens("s1", "new_token", "new_refresh", 3600));
    }
}
