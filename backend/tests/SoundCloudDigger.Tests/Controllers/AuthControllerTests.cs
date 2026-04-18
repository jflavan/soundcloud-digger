using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<ISoundCloudClient> _mockClient = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
    private readonly Mock<IFeedCache> _mockFeedCache = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SoundCloud:ClientId"] = "test_client_id",
                ["SoundCloud:RedirectUri"] = "http://scdigger.localhost:5173/auth/callback",
                ["FrontendUrl"] = "http://scdigger.localhost:5173",
            })
            .Build();

        _sut = new AuthController(config, _mockClient.Object, _mockTokenService.Object, _mockScopeFactory.Object, _mockFeedCache.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    [Fact]
    public void Login_RedirectsToSoundCloudWithPkceParams()
    {
        var result = _sut.Login() as RedirectResult;

        Assert.NotNull(result);
        Assert.Contains("secure.soundcloud.com/authorize", result.Url);
        Assert.Contains("client_id=test_client_id", result.Url);
        Assert.Contains("response_type=code", result.Url);
        Assert.Contains("code_challenge_method=S256", result.Url);
        Assert.Contains("code_challenge=", result.Url);
        Assert.Contains("state=", result.Url);
    }

    [Fact]
    public async Task Callback_ExchangesCodeAndStoresTokens()
    {
        _sut.HttpContext.Session.SetString("pkce_verifier", "test_verifier");
        _sut.HttpContext.Session.SetString("oauth_state", "test_state");

        _mockClient.Setup(c => c.ExchangeCodeForToken("auth_code", "test_verifier", "http://scdigger.localhost:5173/auth/callback"))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = "access_123",
                RefreshToken = "refresh_456",
                ExpiresIn = 3600,
            });

        var result = await _sut.Callback("auth_code", "test_state") as RedirectResult;

        Assert.NotNull(result);
        Assert.Equal("http://scdigger.localhost:5173/feed", result.Url);
        _mockTokenService.Verify(t => t.Store(It.IsAny<string>(), "access_123", "refresh_456", 3600));
    }

    [Fact]
    public async Task Callback_InvalidState_ReturnsBadRequest()
    {
        _sut.HttpContext.Session.SetString("pkce_verifier", "test_verifier");
        _sut.HttpContext.Session.SetString("oauth_state", "correct_state");

        var result = await _sut.Callback("auth_code", "wrong_state");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Logout_CallsSignOutAndClearsSession()
    {
        _sut.HttpContext.Session.SetString("session_id", "s1");
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("access_123", "refresh_456"));

        var result = await _sut.Logout() as OkResult;

        Assert.NotNull(result);
        _mockClient.Verify(c => c.SignOut("access_123"));
        _mockTokenService.Verify(t => t.Remove("s1"));
    }
}

// Minimal in-memory ISession for testing
public class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();
    public string Id => Guid.NewGuid().ToString();
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;
    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
