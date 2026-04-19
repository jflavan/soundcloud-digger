namespace SoundCloudDigger.Api.Services;

public interface IFollowingsService
{
    Task<IReadOnlyList<string>> EnsureAsync(string userUrn);
}
