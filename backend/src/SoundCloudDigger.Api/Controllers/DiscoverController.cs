using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class DiscoverController : ControllerBase
{
    private readonly SessionStore _sessions;
    private readonly DiscoverRepository _repo;
    private readonly IDiscoverFeedService _service;

    public DiscoverController(SessionStore sessions, DiscoverRepository repo,
        IDiscoverFeedService service)
    {
        _sessions = sessions; _repo = repo; _service = service;
    }

    [HttpGet("/api/feed/discover")]
    public IActionResult GetDiscover()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId)) return Unauthorized();
        var session = _sessions.TryGet(sessionId);
        if (session is null) return Unauthorized();

        var tracks = _repo.GetConsensus(session.UserUrn);
        var (fetched, total) = _repo.GetProgress(session.UserUrn);
        var progress = total == 0 ? 1.0 : (double)fetched / total;

        return Ok(new DiscoverResponse
        {
            Tracks = tracks.ToArray(),
            TotalCount = tracks.Count,
            LoadingComplete = _repo.IsLoadingComplete(session.UserUrn),
            LastRefreshedAt = _repo.GetDiscoverLastFetchedAt(session.UserUrn)?.UtcDateTime,
            Progress = progress,
        });
    }

    [HttpPost("/api/feed/discover/refresh")]
    public async Task<IActionResult> RefreshDiscover()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId)) return Unauthorized();
        var session = _sessions.TryGet(sessionId);
        if (session is null) return Unauthorized();

        var enqueued = await _service.RefreshAsync(session.UserUrn);
        if (!enqueued)
        {
            var last = _repo.GetDiscoverLastFetchedAt(session.UserUrn);
            var secsSince = last is null ? 0 : (int)(DateTimeOffset.UtcNow - last.Value).TotalSeconds;
            var retryAfter = Math.Max(1, 120 - secsSince);
            return StatusCode(429, new RefreshResponse { Enqueued = false, RetryAfterSec = retryAfter });
        }

        return Accepted(new RefreshResponse { Enqueued = true });
    }
}
