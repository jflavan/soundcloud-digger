using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Tests.Controllers;

public class FeedControllerTests : IDisposable
{
    private readonly Mock<IFeedCache> _mockCache = new();
    private readonly Mock<IFeedService> _mockFeedService = new();
    private readonly Db _db;
    private readonly FeedController _sut;

    public FeedControllerTests()
    {
        _db = TestDb.Create();
        var sessions = new SessionStore(_db);
        sessions.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));

        // Real scope factory so the self-heal Task.Run can resolve IFeedService.
        var services = new ServiceCollection();
        services.AddSingleton(_mockFeedService.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _sut = new FeedController(_mockCache.Object, sessions, scopeFactory);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("session_id", "s1");
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetFeed_ReturnsCachedTracks()
    {
        var tracks = new List<FeedTrack>
        {
            new() { Title = "Track A", LikesCount = 100 },
            new() { Title = "Track B", LikesCount = 200 },
        };
        _mockCache.Setup(c => c.GetTracks("s1")).Returns(tracks);
        _mockCache.Setup(c => c.IsLoadingComplete("s1")).Returns(true);

        var result = _sut.GetFeed() as OkObjectResult;
        var response = result?.Value as FeedResponse;

        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount);
        Assert.True(response.LoadingComplete);
    }

    [Fact]
    public void GetFeed_NoSession_ReturnsUnauthorized()
    {
        _sut.HttpContext.Session.Clear();

        var result = _sut.GetFeed();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void GetFeed_LoadingInProgress_ReturnsPartialWithFlag()
    {
        _mockCache.Setup(c => c.GetTracks("s1")).Returns([new FeedTrack { Title = "A" }]);
        _mockCache.Setup(c => c.IsLoadingComplete("s1")).Returns(false);

        var result = _sut.GetFeed() as OkObjectResult;
        var response = result?.Value as FeedResponse;

        Assert.NotNull(response);
        Assert.False(response.LoadingComplete);
        Assert.Equal(1, response.TotalCount);
    }

    [Fact]
    public void GetFeed_SessionRowMissing_ReturnsUnauthorized()
    {
        _sut.HttpContext.Session.SetString("session_id", "ghost");

        var result = _sut.GetFeed();

        Assert.IsType<UnauthorizedResult>(result);
        _mockCache.Verify(c => c.GetTracks(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetFeed_LoadingIncomplete_SelfHealsByStartingFetch()
    {
        _mockCache.Setup(c => c.GetTracks("s1")).Returns([]);
        _mockCache.Setup(c => c.IsLoadingComplete("s1")).Returns(false);
        _mockFeedService.Setup(f => f.IsFetchInFlight("s1")).Returns(false);
        var started = new TaskCompletionSource();
        _mockFeedService.Setup(f => f.StartFetchAsync("s1"))
            .Callback(() => started.TrySetResult())
            .Returns(Task.CompletedTask);

        _sut.GetFeed();

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _mockFeedService.Verify(f => f.StartFetchAsync("s1"), Times.Once);
    }

    [Fact]
    public async Task GetFeed_DoesNotRefetchWhenLoadFinishedBeforeSelfHealRan()
    {
        // First read (in the action) says incomplete; by the time the background task
        // re-checks, the fetch has completed. It must not Clear() and start over.
        var rechecked = new TaskCompletionSource();
        _mockCache.Setup(c => c.GetTracks("s1")).Returns([]);
        _mockCache.SetupSequence(c => c.IsLoadingComplete("s1"))
            .Returns(false)
            .Returns(() => { rechecked.TrySetResult(); return true; });
        _mockFeedService.Setup(f => f.IsFetchInFlight("s1")).Returns(false);

        _sut.GetFeed();

        await rechecked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        _mockFeedService.Verify(f => f.StartFetchAsync(It.IsAny<string>()), Times.Never);
    }
}
