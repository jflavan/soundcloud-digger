using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class HealthController : Controller
{
    private readonly SqliteConnection _conn;

    public HealthController(SqliteConnection conn)
    {
        _conn = conn;
    }

    [HttpGet("/api/health/metrics")]
    public IActionResult Metrics()
    {
        var sessions = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM sessions;");
        var users = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users;");
        var tracks = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM tracks;");
        var feedTracks = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM feed_tracks;");
        var followings = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM followings;");
        var artistReposts = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_reposts;");
        var artistsFetched = _conn.ExecuteScalar<long>("SELECT COUNT(*) FROM artist_fetch_state;");

        return Ok(new
        {
            sessions,
            users,
            tracks,
            feedTracks,
            followings,
            artistReposts,
            artistsFetched,
        });
    }
}
