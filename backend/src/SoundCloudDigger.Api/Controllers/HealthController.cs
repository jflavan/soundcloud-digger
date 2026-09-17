using System.Net;
using Dapper;
using Microsoft.AspNetCore.Mvc;
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
    private readonly Db _db;

    public HealthController(Db db)
    {
        _db = db;
    }

    [HttpGet("/api/health/metrics")]
    public IActionResult Metrics()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null || (!IPAddress.IsLoopback(ip)))
            return NotFound();

        using var conn = _db.Open();
        return Ok(new MetricsResponse(
            Sessions: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM sessions;"),
            Users: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users;"),
            Tracks: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM tracks;"),
            FeedTracks: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM feed_tracks;"),
            Followings: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM followings;"),
            ArtistReposts: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_reposts;"),
            ArtistsFetched: conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_fetch_state;")));
    }
}
