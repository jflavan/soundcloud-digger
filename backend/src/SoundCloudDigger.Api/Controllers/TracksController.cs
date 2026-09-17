using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class TracksController : ControllerBase
{
    private readonly SessionStore _sessions;
    private readonly ITokenService _tokens;
    private readonly ISoundCloudClient _client;

    public TracksController(SessionStore sessions, ITokenService tokens, ISoundCloudClient client)
    {
        _sessions = sessions; _tokens = tokens; _client = client;
    }

    // Resolves a track permalink to a directly playable stream URL. Used by the
    // frontend for tracks the SoundCloud embed widget can't play (access=preview):
    // the public API still serves a preview snippet for those, authenticated with
    // the user's token, which the widget's anonymous session never gets.
    [HttpGet("/api/tracks/stream")]
    public async Task<IActionResult> GetStream([FromQuery] string url)
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId)) return Unauthorized();
        var session = _sessions.TryGet(sessionId);
        if (session is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(url)) return BadRequest(new { error = "url is required" });

        var token = await _tokens.GetValidAccessTokenAsync(session.UserUrn);
        if (token is null) return Unauthorized();

        var urn = await _client.ResolveTrackUrn(url, token);
        if (urn is null) return NotFound();

        var streams = await _client.GetTrackStreams(urn, token);
        var full = streams.HttpMp3128Url;
        var chosen = full ?? streams.PreviewMp3128Url;
        if (chosen is null) return NotFound();

        var cdnUrl = await _client.GetStreamRedirect(chosen, token);
        if (cdnUrl is null) return StatusCode(502);

        return Ok(new StreamResponse { Url = cdnUrl, Preview = full is null });
    }
}
