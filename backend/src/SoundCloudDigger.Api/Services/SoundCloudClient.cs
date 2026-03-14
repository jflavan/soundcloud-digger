using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class SoundCloudClient : ISoundCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SoundCloudClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
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

    public async Task<SoundCloudActivitiesResponse> GetFeedTracks(string accessToken, int limit = 200, string? nextHref = null)
    {
        var url = nextHref ?? $"https://api.soundcloud.com/me/feed/tracks?limit={limit}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudActivitiesResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize feed response");
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
}
