using System.Reflection;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Controllers;

public class HealthControllerTests
{
    private Microsoft.Data.Sqlite.SqliteConnection CreateDb()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        return conn;
    }

    [Fact]
    public void Metrics_ReturnsCountsFromAllTables()
    {
        using var conn = CreateDb();
        conn.Execute("INSERT INTO users (urn, username, display_name, fetched_at) VALUES ('u1', 'user1', 'User 1', 0);");
        conn.Execute("INSERT INTO tracks (urn, payload_json, updated_at) VALUES ('t1', '{}', 0), ('t2', '{}', 0);");
        conn.Execute("INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);");

        var controller = new HealthController(conn);
        var result = controller.Metrics() as OkObjectResult;

        Assert.NotNull(result);
        var value = result!.Value!;
        Assert.Equal(1L, ReadProp(value, "users"));
        Assert.Equal(2L, ReadProp(value, "tracks"));
        Assert.Equal(1L, ReadProp(value, "followings"));
        Assert.Equal(0L, ReadProp(value, "sessions"));
    }

    private static long ReadProp(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return (long)prop!.GetValue(obj)!;
    }
}
