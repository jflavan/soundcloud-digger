using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class DiscoverFeedService : IDiscoverFeedService
{
    private static readonly TimeSpan ArtistTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CooldownTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FullResetInterval = TimeSpan.FromDays(7);
    private readonly SqliteConnection _conn;
    private readonly ISoundCloudClient _client;
    private readonly ITokenService _tokens;
    private readonly IFollowingsService _followings;
    private readonly DiscoverRepository _repo;
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public DiscoverFeedService(
        SqliteConnection conn,
        ISoundCloudClient client,
        ITokenService tokens,
        IFollowingsService followings,
        DiscoverRepository repo)
    {
        _conn = conn; _client = client; _tokens = tokens;
        _followings = followings; _repo = repo;
    }

    public async Task StartFetchAsync(string userUrn)
    {
        if (!_inFlight.TryAdd(userUrn, 0)) return;
        try
        {
            var followed = await _followings.EnsureAsync(userUrn);
            var stale = followed.Where(IsStale).ToList();
            if (stale.Count == 0)
            {
                _repo.MarkDiscoverFetched(userUrn);
                return;
            }

            var accessToken = await _tokens.GetValidAccessTokenAsync(userUrn);
            if (accessToken is null) return;

            await Parallel.ForEachAsync(stale,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (artistUrn, ct) => await FetchArtistAsync(artistUrn, accessToken, ct));

            _repo.MarkDiscoverFetched(userUrn);
        }
        finally
        {
            _inFlight.TryRemove(userUrn, out _);
        }
    }

    public Task<bool> RefreshAsync(string userUrn)
    {
        var last = _repo.GetDiscoverLastFetchedAt(userUrn);
        if (last is not null && DateTimeOffset.UtcNow - last.Value < CooldownTtl)
            return Task.FromResult(false);

        _ = Task.Run(() => StartFetchAsync(userUrn));
        return Task.FromResult(true);
    }

    private bool IsStale(string artistUrn)
    {
        var last = _repo.GetArtistLastFetched(artistUrn);
        if (last is null) return true;
        return DateTimeOffset.UtcNow - last.Value > ArtistTtl;
    }

    private async Task FetchArtistAsync(string artistUrn, string accessToken, CancellationToken ct)
    {
        try
        {
            var lastFullReset = _repo.GetArtistLastFullReset(artistUrn);
            var doFullReset = lastFullReset is null
                || DateTimeOffset.UtcNow - lastFullReset.Value >= FullResetInterval;

            var cursor = doFullReset ? null : _repo.GetArtistCursor(artistUrn);
            string? next = null;
            string? newCursor = null;
            var stopWalking = false;
            var seenUrns = doFullReset ? new List<string>() : null;

            do
            {
                var page = await _client.GetUserReposts(artistUrn, accessToken, next);
                foreach (var repost in page.Collection)
                {
                    if (repost.Track is null) continue;
                    if (!doFullReset && repost.Track.PermalinkUrl == cursor)
                    {
                        stopWalking = true;
                        break;
                    }

                    var feedTrack = FeedTrack.FromTrack(repost.Track, repost.CreatedAt);
                    if (!DateTimeOffset.TryParse(repost.CreatedAt, out var reposted))
                        reposted = DateTimeOffset.UtcNow;
                    _repo.UpsertTrackAndRepost(artistUrn, feedTrack, reposted);
                    if (repost.Track.PermalinkUrl is not null)
                        seenUrns?.Add(repost.Track.PermalinkUrl);

                    newCursor ??= repost.Track.PermalinkUrl;
                }
                next = page.NextHref;
            } while (next is not null && !stopWalking);

            // Prune only after a complete walk. On mid-walk failure we fall through to the
            // catch below, leave last_full_reset_at untouched, and retry the whole walk next
            // cycle — better than deleting reposts that might be on a page we never reached.
            if (doFullReset && seenUrns is not null)
                _repo.DeleteRepostsMissingAfterFullReset(artistUrn, seenUrns);

            _repo.UpsertArtistFetchState(artistUrn, newCursor ?? cursor, doFullReset);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _repo.UpsertArtistFetchState(artistUrn, null);
        }
        catch
        {
            // Other failures: don't update last_fetched_at so the artist retries next cycle.
        }
    }
}
