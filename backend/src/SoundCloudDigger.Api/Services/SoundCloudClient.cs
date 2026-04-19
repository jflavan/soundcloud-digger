using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class SoundCloudClient : ISoundCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly SoundCloudRateLimiter _rateLimiter;
    private readonly RetryPolicy _retry;

    public SoundCloudClient(
        HttpClient httpClient,
        IConfiguration config,
        SoundCloudRateLimiter rateLimiter,
        RetryPolicy retry)
    {
        _httpClient = httpClient;
        _config = config;
        _rateLimiter = rateLimiter;
        _retry = retry;
    }

    public async Task<SoundCloudTokenResponse> ExchangeCodeForToken(string code, string codeVerifier, string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _config["SoundCloud:ClientId"]!,
            ["client_secret"] = _config["SoundCloud:ClientSecret"]!,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["code"] = code,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
        {
            Content = content,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudTokenResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize token response");
    }

    public async Task<SoundCloudTokenResponse> RefreshAccessToken(string refreshToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _config["SoundCloud:ClientId"]!,
            ["client_secret"] = _config["SoundCloud:ClientSecret"]!,
            ["refresh_token"] = refreshToken,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
        {
            Content = content,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudTokenResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize token response");
    }

    public async Task SignOut(string accessToken)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { access_token = accessToken }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("https://secure.soundcloud.com/sign-out", content);
        response.EnsureSuccessStatusCode();
    }

    public Task<SoundCloudActivitiesResponse> GetFeedTracks(string accessToken, int limit = 200, string? nextHref = null)
        => AuthedGet<SoundCloudActivitiesResponse>(
            nextHref ?? $"https://api.soundcloud.com/me/feed/tracks?limit={limit}",
            accessToken);

    public Task<SoundCloudUsersResponse> GetFollowings(string accessToken, string? nextHref = null)
        => AuthedGet<SoundCloudUsersResponse>(
            nextHref ?? "https://api.soundcloud.com/me/followings?limit=200&linked_partitioning=true",
            accessToken);

    public Task<SoundCloudRepostsResponse> GetUserReposts(string userUrn, string accessToken, string? nextHref = null)
        => AuthedGet<SoundCloudRepostsResponse>(
            nextHref ?? $"https://api.soundcloud.com/users/{Uri.EscapeDataString(userUrn)}/reposts/tracks?limit=200&linked_partitioning=true",
            accessToken);

    public Task<SoundCloudUser> GetMe(string accessToken)
        => AuthedGet<SoundCloudUser>("https://api.soundcloud.com/me", accessToken);

    private Task<T> AuthedGet<T>(string url, string accessToken)
    {
        return _retry.ExecuteAsync(ct =>
            _rateLimiter.ExecuteAsync(async innerCt =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
                var response = await _httpClient.SendAsync(request, innerCt);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await HandleThrottle(response, innerCt);
                    throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
                }

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<T>(cancellationToken: innerCt);
                return body ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
            }, ct),
            CancellationToken.None);
    }

    private async Task HandleThrottle(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("rate_limit", out var rl)
                && rl.TryGetProperty("reset_time", out var rt)
                && rt.GetString() is string s
                && DateTimeOffset.TryParse(s, out var until))
            {
                _rateLimiter.ReportThrottle(until);
            }
        }
        catch
        {
            _rateLimiter.ReportThrottle(DateTimeOffset.UtcNow.AddSeconds(5));
        }
    }
}
