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
    private static Db CreateDb() => TestDb.Create();

    private static HealthController CreateController(
        Db db, IPAddress? remoteIp)
    {
        var controller = new HealthController(db);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public void Metrics_ReturnsCountsFromAllTables()
    {
        using var db = CreateDb();
        using var conn = db.Open();
        conn.Execute("INSERT INTO users (urn, username, display_name, fetched_at) VALUES ('u1', 'user1', 'User 1', 0);");
        conn.Execute("INSERT INTO tracks (urn, payload_json, updated_at) VALUES ('t1', '{}', 0), ('t2', '{}', 0);");
        conn.Execute("INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', 'a1', 0);");

        var controller = CreateController(db, IPAddress.Loopback);
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
        using var db = CreateDb();
        using var conn = db.Open();
        var controller = CreateController(db, IPAddress.Parse("203.0.113.5"));

        var result = controller.Metrics();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Metrics_AcceptsIPv6Loopback()
    {
        using var db = CreateDb();
        using var conn = db.Open();
        var controller = CreateController(db, IPAddress.IPv6Loopback);

        var result = controller.Metrics();

        Assert.IsType<OkObjectResult>(result);
    }
}
