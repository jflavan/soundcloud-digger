using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

public class SoundCloudRepost
{
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("track")] public SoundCloudTrack? Track { get; set; }
}

public class SoundCloudRepostsResponse
{
    [JsonPropertyName("collection")] public List<SoundCloudRepost> Collection { get; set; } = new();
    [JsonPropertyName("next_href")] public string? NextHref { get; set; }
}
