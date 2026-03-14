using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Helpers;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class AuthController : Controller
{
    private readonly IConfiguration _config;
    private readonly ISoundCloudClient _client;
    private readonly ITokenService _tokenService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFeedCache _feedCache;

    public AuthController(
        IConfiguration config,
        ISoundCloudClient client,
        ITokenService tokenService,
        IServiceScopeFactory scopeFactory,
        IFeedCache feedCache)
    {
        _config = config;
        _client = client;
        _tokenService = tokenService;
        _scopeFactory = scopeFactory;
        _feedCache = feedCache;
    }

    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        HttpContext.Session.SetString("pkce_verifier", verifier);
        HttpContext.Session.SetString("oauth_state", state);

        var clientId = _config["SoundCloud:ClientId"];
        var redirectUri = _config["SoundCloud:RedirectUri"];
        var url = $"https://secure.soundcloud.com/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri!)}&response_type=code&code_challenge={challenge}&code_challenge_method=S256&state={state}";

        return Redirect(url);
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var expectedState = HttpContext.Session.GetString("oauth_state");
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(expectedState) || state != expectedState)
            return BadRequest("Invalid state parameter");

        var verifier = HttpContext.Session.GetString("pkce_verifier")!;
        var redirectUri = _config["SoundCloud:RedirectUri"]!;
        var tokenResponse = await _client.ExchangeCodeForToken(code, verifier, redirectUri);

        var sessionId = Guid.NewGuid().ToString("N");
        _tokenService.Store(sessionId, tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresIn);
        HttpContext.Session.SetString("session_id", sessionId);

        // Use IServiceScopeFactory to avoid scoped service disposal issues
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var feedService = scope.ServiceProvider.GetRequiredService<IFeedService>();
                await feedService.StartFetchAsync(sessionId);
            }
            catch
            {
                // Best effort — feed loading failure is visible to user via loadingComplete staying false
            }
        });

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:5173";
        return Redirect($"{frontendUrl}/feed");
    }

    [HttpPost("/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (sessionId is not null)
        {
            var tokens = _tokenService.Get(sessionId);
            if (tokens is not null)
            {
                try { await _client.SignOut(tokens.Value.AccessToken); }
                catch { /* best effort */ }
            }
            _tokenService.Remove(sessionId);
            _feedCache.Clear(sessionId);
        }
        HttpContext.Session.Clear();
        return Ok();
    }
}
