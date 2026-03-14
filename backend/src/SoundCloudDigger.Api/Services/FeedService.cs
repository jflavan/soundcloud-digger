using System.Collections.Concurrent;
using System.Net;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class FeedService : IFeedService
{
    private readonly ISoundCloudClient _client;
    private readonly IFeedCache _cache;
    private readonly ITokenService _tokenService;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();
    private const int MaxTracks = 10_000;
    private const int MaxRetries = 3;

    public FeedService(ISoundCloudClient client, IFeedCache cache, ITokenService tokenService)
    {
        _client = client;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task StartFetchAsync(string sessionId)
    {
        var accessToken = await GetValidAccessToken(sessionId);
        if (accessToken is null) return;

        _cache.Clear(sessionId);
        var cutoff = DateTime.UtcNow.AddDays(-30);
        string? nextHref = null;
        var totalFetched = 0;
        var tokenHolder = new TokenHolder(accessToken);

        while (totalFetched < MaxTracks)
        {
            SoundCloudActivitiesResponse response;
            try
            {
                response = await FetchWithRetryAndRefresh(sessionId, tokenHolder, nextHref);
            }
            catch (HttpRequestException)
            {
                break;
            }

            if (response.Collection.Count == 0) break;

            var tracks = response.Collection
                .Where(a => a.Origin is not null)
                .Select(FeedTrack.FromActivity)
                .ToList();

            _cache.AddTracks(sessionId, tracks);
            totalFetched += tracks.Count;

            var allOlderThanCutoff = response.Collection
                .All(a => a.CreatedAt < cutoff);

            if (allOlderThanCutoff || response.NextHref is null) break;

            nextHref = response.NextHref;
        }

        _cache.SetLoadingComplete(sessionId, true);
    }

    public async Task RefreshAsync(string sessionId)
    {
        var accessToken = await GetValidAccessToken(sessionId);
        if (accessToken is null) return;

        var existingTracks = _cache.GetTracks(sessionId);
        var existingUrls = new HashSet<string>(
            existingTracks.Where(t => t.PermalinkUrl is not null).Select(t => t.PermalinkUrl!));

        string? nextHref = null;
        var foundExisting = false;
        var tokenHolder = new TokenHolder(accessToken);

        while (!foundExisting)
        {
            SoundCloudActivitiesResponse response;
            try
            {
                response = await FetchWithRetryAndRefresh(sessionId, tokenHolder, nextHref);
            }
            catch (HttpRequestException)
            {
                break;
            }

            if (response.Collection.Count == 0) break;

            var newTracks = new List<FeedTrack>();
            foreach (var activity in response.Collection.Where(a => a.Origin is not null))
            {
                var track = FeedTrack.FromActivity(activity);
                if (track.PermalinkUrl is not null && existingUrls.Contains(track.PermalinkUrl))
                {
                    foundExisting = true;
                    break;
                }
                newTracks.Add(track);
            }

            if (newTracks.Count > 0)
                _cache.AddTracks(sessionId, newTracks);

            if (response.NextHref is null) break;
            nextHref = response.NextHref;
        }
    }

    private async Task<string?> GetValidAccessToken(string sessionId)
    {
        if (!_tokenService.IsExpired(sessionId))
            return _tokenService.Get(sessionId)?.AccessToken;

        var semaphore = _refreshLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            // Re-check after acquiring lock — another caller may have already refreshed
            if (!_tokenService.IsExpired(sessionId))
                return _tokenService.Get(sessionId)?.AccessToken;

            var tokens = _tokenService.Get(sessionId);
            if (tokens is null) return null;

            try
            {
                var refreshed = await _client.RefreshAccessToken(tokens.Value.RefreshToken);
                _tokenService.UpdateTokens(sessionId, refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresIn);
                return refreshed.AccessToken;
            }
            catch
            {
                return null;
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<SoundCloudActivitiesResponse> FetchWithRetryAndRefresh(
        string sessionId, TokenHolder tokenHolder, string? nextHref)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await _client.GetFeedTracks(tokenHolder.AccessToken, 200, nextHref);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                var newToken = await GetValidAccessToken(sessionId);
                if (newToken is null || newToken == tokenHolder.AccessToken) throw;
                tokenHolder.AccessToken = newToken;
                continue;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    private class TokenHolder(string accessToken)
    {
        public string AccessToken { get; set; } = accessToken;
    }
}
