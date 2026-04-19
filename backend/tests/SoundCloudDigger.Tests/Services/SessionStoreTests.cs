using Dapper;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class SessionStoreTests
{
    private Microsoft.Data.Sqlite.SqliteConnection CreateDb()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        return conn;
    }

    [Fact]
    public void Create_InsertsRow()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);

        store.Create("sess1", "soundcloud:users:1", "at", "rt",
            DateTimeOffset.UtcNow.AddHours(1));

        var count = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sessions WHERE session_id='sess1';");
        Assert.Equal(1, count);
    }

    [Fact]
    public void TryGet_ReturnsSession()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);
        store.Create("sess1", "soundcloud:users:1", "at", "rt",
            DateTimeOffset.UtcNow.AddHours(1));

        var session = store.TryGet("sess1");

        Assert.NotNull(session);
        Assert.Equal("soundcloud:users:1", session!.UserUrn);
    }

    [Fact]
    public void TryGet_ReturnsNullForUnknownSession()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);

        var session = store.TryGet("missing");

        Assert.Null(session);
    }

    [Fact]
    public void UpdateTokens_ReplacesAccessAndRefresh()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);
        store.Create("sess1", "soundcloud:users:1", "at1", "rt1",
            DateTimeOffset.UtcNow.AddHours(1));

        var newExpiry = DateTimeOffset.UtcNow.AddHours(2);
        store.UpdateTokens("soundcloud:users:1", "at2", "rt2", newExpiry);

        var session = store.TryGet("sess1")!;
        Assert.Equal("at2", session.AccessToken);
        Assert.Equal("rt2", session.RefreshToken);
    }

    [Fact]
    public void Delete_RemovesRow()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);
        store.Create("sess1", "soundcloud:users:1", "at", "rt",
            DateTimeOffset.UtcNow.AddHours(1));

        store.Delete("sess1");

        Assert.Null(store.TryGet("sess1"));
    }

    [Fact]
    public void GetActiveSessions_ReturnsAll()
    {
        using var conn = CreateDb();
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        store.Create("s2", "u2", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));

        var active = store.GetActiveSessions().ToList();

        Assert.Equal(2, active.Count);
    }
}
