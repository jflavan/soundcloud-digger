using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly Mock<ISoundCloudClient> _mockClient = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
    private readonly Mock<IFeedCache> _mockFeedCache = new();
    private readonly Mock<IDiscoverFeedService> _mockDiscoverService = new();
    private readonly SqliteConnection _db;
    private readonly SessionStore _sessionStore;
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

        _db = Db.OpenInMemory();
        SchemaMigrator.Migrate(_db, new IMigration[] { new V1_InitialSchema() });
        _sessionStore = new SessionStore(_db);

        _mockDiscoverService
            .Setup(d => d.StartFetchAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sut = new AuthController(
            config,
            _mockClient.Object,
            _mockTokenService.Object,
            _mockScopeFactory.Object,
            _mockFeedCache.Object,
            _sessionStore,
            _db,
            _mockDiscoverService.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    public void Dispose() => _db.Dispose();

    private void SetupCallbackMocks(
        string accessToken = "access_123",
        string refreshToken = "refresh_456",
        int expiresIn = 3600,
        string userUrn = "soundcloud:users:99",
        string username = "testuser")
    {
        _mockClient
            .Setup(c => c.ExchangeCodeForToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
            });

        _mockClient
            .Setup(c => c.GetMe(accessToken))
            .ReturnsAsync(new SoundCloudUser
            {
                Urn = userUrn,
                Username = username,
                PermalinkUrl = $"https://soundcloud.com/{username}",
            });
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
        _mockClient.Setup(c => c.GetMe("access_123"))
            .ReturnsAsync(new SoundCloudUser
            {
                Urn = "soundcloud:users:99",
                Username = "testuser",
                PermalinkUrl = "https://soundcloud.com/testuser",
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

    [Fact]
    public async Task Callback_UpsertsUserAndCreatesSession()
    {
        _sut.HttpContext.Session.SetString("pkce_verifier", "verifier1");
        _sut.HttpContext.Session.SetString("oauth_state", "state1");
        SetupCallbackMocks(
            accessToken: "at_upsert",
            refreshToken: "rt_upsert",
            expiresIn: 3600,
            userUrn: "soundcloud:users:42",
            username: "digger_user");

        await _sut.Callback("code1", "state1");

        // Assert: user row upserted
        var userCount = _db.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM users WHERE urn='soundcloud:users:42';");
        Assert.Equal(1, userCount);

        var storedUsername = _db.ExecuteScalar<string>(
            "SELECT username FROM users WHERE urn='soundcloud:users:42';");
        Assert.Equal("digger_user", storedUsername);

        // Assert: session row created with correct user_urn
        var sessionCount = _db.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sessions WHERE user_urn='soundcloud:users:42';");
        Assert.Equal(1, sessionCount);

        // Assert: session_id in HTTP session matches sessions table
        var sessionId = _sut.HttpContext.Session.GetString("session_id");
        Assert.NotNull(sessionId);
        var sessionRecord = _sessionStore.TryGet(sessionId!);
        Assert.NotNull(sessionRecord);
        Assert.Equal("soundcloud:users:42", sessionRecord!.UserUrn);
        Assert.Equal("at_upsert", sessionRecord.AccessToken);
    }

    [Fact]
    public async Task Logout_DeletesSessionStoreRow()
    {
        // Arrange: go through callback to create session and user rows
        _sut.HttpContext.Session.SetString("pkce_verifier", "verifier2");
        _sut.HttpContext.Session.SetString("oauth_state", "state2");
        SetupCallbackMocks(
            accessToken: "at_logout",
            refreshToken: "rt_logout",
            expiresIn: 3600,
            userUrn: "soundcloud:users:55",
            username: "logout_user");
        _mockClient.Setup(c => c.SignOut(It.IsAny<string>())).Returns(Task.CompletedTask);

        await _sut.Callback("code2", "state2");

        var sessionId = _sut.HttpContext.Session.GetString("session_id");
        Assert.NotNull(sessionId);

        // Confirm session row exists before logout
        var before = _sessionStore.TryGet(sessionId!);
        Assert.NotNull(before);

        // Set up tokenService for logout
        _mockTokenService.Setup(t => t.Get(sessionId!)).Returns(("at_logout", "rt_logout"));

        // Act
        await _sut.Logout();

        // Assert: session row removed
        var after = _sessionStore.TryGet(sessionId!);
        Assert.Null(after);
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
