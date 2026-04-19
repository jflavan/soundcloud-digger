using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Controllers;

public class DiscoverControllerTests
{
    private Microsoft.Data.Sqlite.SqliteConnection CreateDb()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        return conn;
    }

    // Match the existing FeedController pattern: it reads session via
    // HttpContext.Session.GetString("session_id"). Tests need a mock
    // ISession implementation. Read FeedController tests for the setup
    // pattern and reuse it here.

    [Fact]
    public void GetDiscover_ReturnsUnauthorized_WithoutSession()
    {
        using var conn = CreateDb();
        var discoverSvc = new Mock<IDiscoverFeedService>();
        var controller = new DiscoverController(
            new SessionStore(conn), new DiscoverRepository(conn), discoverSvc.Object)
        {
            ControllerContext = WithSessionId(null),
        };

        var result = controller.GetDiscover();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RefreshDiscover_Returns429InsideCooldown()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        var repo = new DiscoverRepository(conn);
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
