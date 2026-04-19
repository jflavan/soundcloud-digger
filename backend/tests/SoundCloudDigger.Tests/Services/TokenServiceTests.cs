using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut = new();

    [Fact]
    public void StoreAndRetrieve_ReturnsStoredTokens()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", 3600);
        var tokens = _sut.Get("session1");
        Assert.NotNull(tokens);
        Assert.Equal("access_abc", tokens.Value.AccessToken);
        Assert.Equal("refresh_xyz", tokens.Value.RefreshToken);
    }

    [Fact]
    public void Get_UnknownSession_ReturnsNull()
    {
        var tokens = _sut.Get("nonexistent");
        Assert.Null(tokens);
    }

    [Fact]
    public void UpdateRefreshToken_ReplacesOldToken()
    {
        _sut.Store("session1", "access_abc", "refresh_old", 3600);
        _sut.UpdateTokens("session1", "access_new", "refresh_new", 3600);
        var tokens = _sut.Get("session1");
        Assert.NotNull(tokens);
        Assert.Equal("access_new", tokens.Value.AccessToken);
        Assert.Equal("refresh_new", tokens.Value.RefreshToken);
    }

    [Fact]
    public void Remove_ClearsSession()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", 3600);
        _sut.Remove("session1");
        Assert.Null(_sut.Get("session1"));
    }

    [Fact]
    public void IsExpired_ReturnsTrueForExpiredToken()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", expiresIn: 0);
        Assert.True(_sut.IsExpired("session1"));
    }

    [Fact]
    public void IsExpired_ReturnsFalseForValidToken()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", expiresIn: 3600);
        Assert.False(_sut.IsExpired("session1"));
    }

    // ── GetValidAccessTokenAsync tests ────────────────────────────────────────

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsExisting_WhenNotExpired()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "goodtoken", "rt", DateTimeOffset.UtcNow.AddMinutes(30));

        var client = new Mock<ISoundCloudClient>();
        var svc = CreateTokenService(store, client.Object);

        var token = await svc.GetValidAccessTokenAsync("u1");

        Assert.Equal("goodtoken", token);
        client.Verify(c => c.RefreshAccessToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_RefreshesWhenExpired()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "stale", "rt", DateTimeOffset.UtcNow.AddMinutes(-5));

        var client = new Mock<ISoundCloudClient>();
        client.Setup(c => c.RefreshAccessToken("rt"))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = "fresh", RefreshToken = "rt2", ExpiresIn = 3600,
            });

        var svc = CreateTokenService(store, client.Object);

        var token = await svc.GetValidAccessTokenAsync("u1");

        Assert.Equal("fresh", token);
        Assert.Equal("fresh", store.TryGet("s1")!.AccessToken);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsNullWhenNoSession()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        var store = new SessionStore(conn);

        var client = new Mock<ISoundCloudClient>();
        var svc = CreateTokenService(store, client.Object);

        var token = await svc.GetValidAccessTokenAsync("unknown-user");

        Assert.Null(token);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsNullWhenRefreshFails()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
        var store = new SessionStore(conn);
        store.Create("s1", "u1", "stale", "rt", DateTimeOffset.UtcNow.AddMinutes(-5));

        var client = new Mock<ISoundCloudClient>();
        client.Setup(c => c.RefreshAccessToken(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("revoked"));

        var svc = CreateTokenService(store, client.Object);

        var token = await svc.GetValidAccessTokenAsync("u1");

        Assert.Null(token);
    }

    private static TokenService CreateTokenService(SessionStore store, ISoundCloudClient client)
    {
        // Build a minimal IServiceProvider that can resolve ISoundCloudClient.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(client);
        var sp = services.BuildServiceProvider();
        return new TokenService(store, sp);
    }
}
