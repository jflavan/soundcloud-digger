using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

// The /users/{urn}/reposts/tracks endpoint returns flat SoundCloudTrack objects —
// not a repost envelope. There is no repost timestamp on the response; use the
// track's own created_at as a proxy for when it was reposted.
public class SoundCloudRepostsResponse
{
    [JsonPropertyName("collection")] public List<SoundCloudTrack> Collection { get; set; } = new();
    [JsonPropertyName("next_href")] public string? NextHref { get; set; }
}
