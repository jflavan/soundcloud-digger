using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public interface ISoundCloudClient
{
    Task<SoundCloudTokenResponse> ExchangeCodeForToken(string code, string codeVerifier, string redirectUri);
    Task<SoundCloudTokenResponse> RefreshAccessToken(string refreshToken);
    Task<SoundCloudActivitiesResponse> GetFeedTracks(string accessToken, int limit = 200, string? nextHref = null);
    Task SignOut(string accessToken);
    Task<SoundCloudUsersResponse> GetFollowings(string accessToken, string? nextHref = null);
    Task<SoundCloudRepostsResponse> GetUserReposts(string userUrn, string accessToken, string? nextHref = null);
}
