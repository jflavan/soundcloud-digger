using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public interface IFeedCache
{
    List<FeedTrack> GetTracks(string sessionId);
    void AddTracks(string sessionId, List<FeedTrack> tracks);
    bool IsLoadingComplete(string sessionId);
    void SetLoadingComplete(string sessionId, bool complete);
    void Clear(string sessionId);
    List<string> GetActiveSessionIds();
}
