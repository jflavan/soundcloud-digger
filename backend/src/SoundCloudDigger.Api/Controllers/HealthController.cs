using System.Net;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Api.Controllers;

public record MetricsResponse(
    long Sessions,
    long Users,
    long Tracks,
    long FeedTracks,
    long Followings,
    long ArtistReposts,
    long ArtistsFetched);

[ApiController]
public class HealthController : Controller
{
    private readonly SqliteConnection _conn;
    private readonly DbLock _dbLock;

    public HealthController(SqliteConnection conn, DbLock dbLock)
    {
        _conn = conn;
        _dbLock = dbLock;
    }

    [HttpGet("/api/health/metrics")]
    public IActionResult Metrics()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null || (!IPAddress.IsLoopback(ip)))
            return NotFound();

        using var _ = _dbLock.Acquire();
        return Ok(new MetricsResponse(
            Sessions: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM sessions;"),
            Users: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users;"),
            Tracks: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM tracks;"),
            FeedTracks: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM feed_tracks;"),
            Followings: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM followings;"),
            ArtistReposts: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_reposts;"),
            ArtistsFetched: _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_fetch_state;")));
    }
}
