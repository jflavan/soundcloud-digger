namespace SoundCloudDigger.Api.Models;

public class DiscoverResponse
{
    public DiscoverTrack[] Tracks { get; set; } = Array.Empty<DiscoverTrack>();
    public int TotalCount { get; set; }
    public bool LoadingComplete { get; set; }
    public DateTime? LastRefreshedAt { get; set; }
    public double Progress { get; set; }
}

public class RefreshResponse
{
    public bool Enqueued { get; set; }
    public int? RetryAfterSec { get; set; }
}
