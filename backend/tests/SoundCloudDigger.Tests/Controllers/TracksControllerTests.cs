using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Tests.Controllers;

public class TracksControllerTests : IDisposable
{
    private const string Permalink = "https://soundcloud.com/artist/song";
    private readonly Mock<ISoundCloudClient> _client = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Db _db = TestDb.Create();
    private readonly TracksController _sut;

    public TracksControllerTests()
    {
        var sessions = new SessionStore(_db);
        sessions.Create("s1", "u1", "at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        _tokens.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync("token");

        _sut = new TracksController(sessions, _tokens.Object, _client.Object);
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetString("session_id", "s1");
        _sut.ControllerContext = new ControllerContext { HttpContext = http };
    }

    public void Dispose() => _db.Dispose();

    private void SetupHappyPath(string? fullUrl, string? previewUrl)
    {
        _client.Setup(c => c.ResolveTrackUrn(Permalink, "token")).ReturnsAsync("soundcloud:tracks:1");
        _client.Setup(c => c.GetTrackStreams("soundcloud:tracks:1", "token"))
            .ReturnsAsync(new SoundCloudStreams { HttpMp3128Url = fullUrl, PreviewMp3128Url = previewUrl });
        _client.Setup(c => c.GetStreamRedirect(It.IsAny<string>(), "token"))
            .ReturnsAsync((string u, string _) => "https://cdn/" + u.Split('/').Last());
    }

    [Fact]
    public async Task NoSession_ReturnsUnauthorized()
    {
        _sut.HttpContext.Session.Clear();
        Assert.IsType<UnauthorizedResult>(await _sut.GetStream(Permalink));
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        _tokens.Setup(t => t.GetValidAccessTokenAsync("u1")).ReturnsAsync((string?)null);
        Assert.IsType<UnauthorizedResult>(await _sut.GetStream(Permalink));
    }

    [Fact]
    public async Task MissingUrl_ReturnsBadRequest()
    {
        Assert.IsType<BadRequestObjectResult>(await _sut.GetStream(""));
    }

    [Fact]
    public async Task UnresolvableTrack_ReturnsNotFound()
    {
        _client.Setup(c => c.ResolveTrackUrn(Permalink, "token")).ReturnsAsync((string?)null);
        Assert.IsType<NotFoundResult>(await _sut.GetStream(Permalink));
    }

    [Fact]
    public async Task NoStreams_ReturnsNotFound()
    {
        SetupHappyPath(fullUrl: null, previewUrl: null);
        Assert.IsType<NotFoundResult>(await _sut.GetStream(Permalink));
    }

    [Fact]
    public async Task PreviewOnlyTrack_ReturnsCdnUrlFlaggedAsPreview()
    {
        SetupHappyPath(fullUrl: null, previewUrl: "https://api/streams/prev");

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStream(Permalink));
        var body = Assert.IsType<StreamResponse>(ok.Value);
        Assert.Equal("https://cdn/prev", body.Url);
        Assert.True(body.Preview);
    }

    [Fact]
    public async Task FullyStreamableTrack_PrefersTheFullStream()
    {
        SetupHappyPath(fullUrl: "https://api/streams/full", previewUrl: "https://api/streams/prev");

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStream(Permalink));
        var body = Assert.IsType<StreamResponse>(ok.Value);
        Assert.Equal("https://cdn/full", body.Url);
        Assert.False(body.Preview);
    }

    [Fact]
    public async Task StreamWithoutRedirect_ReturnsBadGateway()
    {
        SetupHappyPath(fullUrl: null, previewUrl: "https://api/streams/prev");
        _client.Setup(c => c.GetStreamRedirect(It.IsAny<string>(), "token")).ReturnsAsync((string?)null);

        var result = Assert.IsType<StatusCodeResult>(await _sut.GetStream(Permalink));
        Assert.Equal(502, result.StatusCode);
    }
}
