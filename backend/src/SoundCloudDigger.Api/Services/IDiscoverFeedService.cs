namespace SoundCloudDigger.Api.Services;

public interface IDiscoverFeedService
{
    Task StartFetchAsync(string userUrn);
    Task<bool> RefreshAsync(string userUrn);
}
