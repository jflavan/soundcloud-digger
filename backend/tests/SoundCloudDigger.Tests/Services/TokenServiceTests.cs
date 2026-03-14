using SoundCloudDigger.Api.Services;

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
}
