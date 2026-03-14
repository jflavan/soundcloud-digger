using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class FeedController : Controller
{
    private readonly IFeedCache _cache;

    public FeedController(IFeedCache cache)
    {
        _cache = cache;
    }

    [HttpGet("/api/feed")]
    public IActionResult GetFeed()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId))
            return Unauthorized();

        var tracks = _cache.GetTracks(sessionId);
        var loadingComplete = _cache.IsLoadingComplete(sessionId);

        return Ok(new FeedResponse
        {
            Tracks = tracks,
            TotalCount = tracks.Count,
            LoadingComplete = loadingComplete,
        });
    }
}
