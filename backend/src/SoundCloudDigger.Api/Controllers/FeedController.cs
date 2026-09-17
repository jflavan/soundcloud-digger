using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class FeedController : Controller
{
    private readonly IFeedCache _cache;
    private readonly SessionStore _sessions;
    private readonly IServiceScopeFactory _scopeFactory;

    public FeedController(IFeedCache cache, SessionStore sessions, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _sessions = sessions;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("/api/feed")]
    public IActionResult GetFeed()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId))
            return Unauthorized();
        // A cookie can outlive its sessions row (logout in another tab, DB reset).
        // Without this the client would poll a permanent spinner and we'd spawn a
        // doomed fetch on every poll.
        if (_sessions.TryGet(sessionId) is null)
            return Unauthorized();

        var tracks = _cache.GetTracks(sessionId);
        var loadingComplete = _cache.IsLoadingComplete(sessionId);

        // Self-heal: if the session exists but never finished loading (e.g. the fetch
        // crashed before marking complete), kick it off again. Ignored if already running.
        if (!loadingComplete)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var feedService = scope.ServiceProvider.GetRequiredService<IFeedService>();
                    // Re-check both flags here: a fetch that finished between the read
                    // above and now is neither in flight nor incomplete, and re-running
                    // StartFetchAsync would Clear() the feed it just built.
                    if (feedService.IsFetchInFlight(sessionId) || _cache.IsLoadingComplete(sessionId)) return;
                    await feedService.StartFetchAsync(sessionId);
                }
                catch { }
            });
        }

        return Ok(new FeedResponse
        {
            Tracks = tracks,
            TotalCount = tracks.Count,
            LoadingComplete = loadingComplete,
        });
    }
}
