namespace SoundCloudDigger.Api.Models;

public class DiscoverTrack : FeedTrack
{
    public int ReposterCount { get; set; }
    public string[] Reposters { get; set; } = Array.Empty<string>();
    public DateTime LastRepostedAt { get; set; }
}
