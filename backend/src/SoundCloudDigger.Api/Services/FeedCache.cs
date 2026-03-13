using System.Collections.Concurrent;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class FeedCache : IFeedCache
{
    private readonly ConcurrentDictionary<string, UserCache> _cache = new();

    public List<FeedTrack> GetTracks(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var userCache))
        {
            lock (userCache.Lock)
            {
                return [.. userCache.Tracks];
            }
        }
        return [];
    }

    public void AddTracks(string sessionId, List<FeedTrack> tracks)
    {
        var userCache = _cache.GetOrAdd(sessionId, _ => new UserCache());
        lock (userCache.Lock)
        {
            userCache.Tracks.AddRange(tracks);
        }
    }

    public bool IsLoadingComplete(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var userCache))
            return userCache.LoadingComplete;
        return false;
    }

    public void SetLoadingComplete(string sessionId, bool complete)
    {
        var userCache = _cache.GetOrAdd(sessionId, _ => new UserCache());
        userCache.LoadingComplete = complete;
    }

    public void Clear(string sessionId)
    {
        _cache.TryRemove(sessionId, out _);
    }

    public List<string> GetActiveSessionIds()
    {
        return [.. _cache.Keys];
    }

    private class UserCache
    {
        public readonly object Lock = new();
        public List<FeedTrack> Tracks { get; } = [];
        public volatile bool LoadingComplete;
    }
}
