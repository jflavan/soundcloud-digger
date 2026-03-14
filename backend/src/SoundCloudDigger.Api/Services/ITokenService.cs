namespace SoundCloudDigger.Api.Services;

public interface ITokenService
{
    void Store(string sessionId, string accessToken, string refreshToken, int expiresIn);
    (string AccessToken, string RefreshToken)? Get(string sessionId);
    void UpdateTokens(string sessionId, string accessToken, string refreshToken, int expiresIn);
    void Remove(string sessionId);
    bool IsExpired(string sessionId);
}
