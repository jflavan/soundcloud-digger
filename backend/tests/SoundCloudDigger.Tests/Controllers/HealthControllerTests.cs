using System.Net;
using Dapper;
using Microsoft.AspNetCore.Http;
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

    private static HealthController CreateController(
        Microsoft.Data.Sqlite.SqliteConnection conn, IPAddress? remoteIp)
    {
        var controller = new HealthController(conn, new DbLock());
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public void Metrics_ReturnsCountsFromAllTables()
    {
        using var conn = CreateDb();
        conn.Execute("INSERT INTO users (urn, username, display_name, fetched_at) VALUES ('u1', 'user1', 'User 1', 0);");
        conn.Execute("INSERT INTO tracks (urn, payload_json, updated_at) VALUES ('t1', '{}', 0), ('t2', '{}', 0);");
        conn.Execute("INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);");

        var controller = CreateController(conn, IPAddress.Loopback);
        var result = controller.Metrics() as OkObjectResult;

        Assert.NotNull(result);
        var metrics = Assert.IsType<MetricsResponse>(result!.Value);
        Assert.Equal(1L, metrics.Users);
        Assert.Equal(2L, metrics.Tracks);
        Assert.Equal(1L, metrics.Followings);
        Assert.Equal(0L, metrics.Sessions);
    }

    [Fact]
    public void Metrics_Returns404ForNonLoopbackRequest()
    {
        using var conn = CreateDb();
        var controller = CreateController(conn, IPAddress.Parse("203.0.113.5"));

        var result = controller.Metrics();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Metrics_AcceptsIPv6Loopback()
    {
        using var conn = CreateDb();
        var controller = CreateController(conn, IPAddress.IPv6Loopback);

        var result = controller.Metrics();

        Assert.IsType<OkObjectResult>(result);
    }
}
