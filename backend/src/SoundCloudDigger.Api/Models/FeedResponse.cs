namespace SoundCloudDigger.Api.Models;

public class FeedResponse
{
    public List<FeedTrack> Tracks { get; set; } = [];
    public int TotalCount { get; set; }
    public bool LoadingComplete { get; set; }
}
