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
    Task<SoundCloudUser> GetMe(string accessToken);

    /// <summary>Resolves a track permalink to its URN, or null if SoundCloud can't find it.</summary>
    Task<string?> ResolveTrackUrn(string permalinkUrl, string accessToken);
    Task<SoundCloudStreams> GetTrackStreams(string trackUrn, string accessToken);
    /// <summary>Follows a stream URL one hop to its signed CDN location; null if it didn't redirect.</summary>
    Task<string?> GetStreamRedirect(string streamUrl, string accessToken);
}
