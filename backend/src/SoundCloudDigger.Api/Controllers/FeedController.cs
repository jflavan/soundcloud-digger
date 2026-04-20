using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class FeedController : Controller
{
    private readonly IFeedCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public FeedController(IFeedCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    [HttpGet("/api/feed")]
    public IActionResult GetFeed()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId))
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
                    if (feedService.IsFetchInFlight(sessionId)) return;
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
