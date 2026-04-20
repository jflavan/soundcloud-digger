namespace SoundCloudDigger.Api.Services;

public interface IFeedService
{
    Task StartFetchAsync(string sessionId);
    Task RefreshAsync(string sessionId);
    bool IsFetchInFlight(string sessionId);
}
