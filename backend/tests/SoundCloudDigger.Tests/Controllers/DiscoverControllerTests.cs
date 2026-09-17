using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Controllers;

public class DiscoverControllerTests
{
    private static Db CreateDb() => TestDb.Create();

    // Match the existing FeedController pattern: it reads session via
    // HttpContext.Session.GetString("session_id"). Tests need a mock
    // ISession implementation. Read FeedController tests for the setup
    // pattern and reuse it here.

    [Fact]
    public void GetDiscover_ReturnsUnauthorized_WithoutSession()
    {
        using var db = CreateDb();
        var discoverSvc = new Mock<IDiscoverFeedService>();
        var controller = new DiscoverController(
            new SessionStore(db), new DiscoverRepository(db), discoverSvc.Object)
        {
            ControllerContext = WithSessionId(null),
        };

        var result = controller.GetDiscover();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RefreshDiscover_Returns429InsideCooldown()
    {
        using var db = CreateDb();
        var store = new SessionStore(db);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        var repo = new DiscoverRepository(db);
        repo.MarkDiscoverFetched("u1");

        var discoverSvc = new Mock<IDiscoverFeedService>();
        discoverSvc.Setup(s => s.RefreshAsync("u1")).ReturnsAsync(false);

        var controller = new DiscoverController(store, repo, discoverSvc.Object)
        {
            ControllerContext = WithSessionId("s1"),
        };

        var result = await controller.RefreshDiscover();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(429, objectResult.StatusCode);
    }

    [Fact]
    public void GetDiscover_ReturnsUnauthorized_WhenSessionIdUnknown()
    {
        using var db = CreateDb();
        var controller = new DiscoverController(
            new SessionStore(db), new DiscoverRepository(db), new Mock<IDiscoverFeedService>().Object)
        {
            ControllerContext = WithSessionId("nope"),
        };

        Assert.IsType<UnauthorizedResult>(controller.GetDiscover());
    }

    [Fact]
    public void GetDiscover_ReturnsResponse_ForValidSession()
    {
        using var db = CreateDb();
        var store = new SessionStore(db);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        var repo = new DiscoverRepository(db);
        repo.MarkDiscoverFetched("u1");

        var controller = new DiscoverController(store, repo, new Mock<IDiscoverFeedService>().Object)
        {
            ControllerContext = WithSessionId("s1"),
        };

        var ok = Assert.IsType<OkObjectResult>(controller.GetDiscover());
        var body = Assert.IsType<DiscoverResponse>(ok.Value);
        Assert.Empty(body.Tracks);
        Assert.Equal(0, body.TotalCount);
        Assert.Equal(1.0, body.Progress); // no followings yet → treated as fully fetched
        Assert.NotNull(body.LastRefreshedAt);
    }

    [Fact]
    public async Task RefreshDiscover_ReturnsUnauthorized_WithoutSession()
    {
        using var db = CreateDb();
        var controller = new DiscoverController(
            new SessionStore(db), new DiscoverRepository(db), new Mock<IDiscoverFeedService>().Object)
        {
            ControllerContext = WithSessionId(null),
        };

        Assert.IsType<UnauthorizedResult>(await controller.RefreshDiscover());
    }

    [Fact]
    public async Task RefreshDiscover_ReturnsAccepted_WhenEnqueued()
    {
        using var db = CreateDb();
        var store = new SessionStore(db);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        var discoverSvc = new Mock<IDiscoverFeedService>();
        discoverSvc.Setup(s => s.RefreshAsync("u1")).ReturnsAsync(true);

        var controller = new DiscoverController(store, new DiscoverRepository(db), discoverSvc.Object)
        {
            ControllerContext = WithSessionId("s1"),
        };

        var accepted = Assert.IsType<AcceptedResult>(await controller.RefreshDiscover());
        var body = Assert.IsType<RefreshResponse>(accepted.Value);
        Assert.True(body.Enqueued);
        Assert.Null(body.RetryAfterSec);
    }

    [Fact]
    public async Task RefreshDiscover_429UsesFullCooldown_WhenNeverFetched()
    {
        using var db = CreateDb();
        var store = new SessionStore(db);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        var discoverSvc = new Mock<IDiscoverFeedService>();
        discoverSvc.Setup(s => s.RefreshAsync("u1")).ReturnsAsync(false);

        var controller = new DiscoverController(store, new DiscoverRepository(db), discoverSvc.Object)
        {
            ControllerContext = WithSessionId("s1"),
        };

        var result = Assert.IsType<ObjectResult>(await controller.RefreshDiscover());
        Assert.Equal(429, result.StatusCode);
        var body = Assert.IsType<RefreshResponse>(result.Value);
        Assert.False(body.Enqueued);
        Assert.Equal(120, body.RetryAfterSec);
    }

    private static ControllerContext WithSessionId(string? sessionId)
    {
        var session = new StubSession();
        if (sessionId is not null) session.SetString("session_id", sessionId);
        var http = new DefaultHttpContext { Session = session };
        return new ControllerContext { HttpContext = http };
    }

    private sealed class StubSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public string Id => "test-session";
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value)
        {
            if (_store.TryGetValue(key, out var v)) { value = v; return true; }
            value = Array.Empty<byte>(); return false;
        }
    }
}
