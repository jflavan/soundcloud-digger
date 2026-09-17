using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

// GET /tracks/{urn}/streams. Which keys are present depends on the track's
// `access` for this user: 'playable' tracks get the full mp3/hls/opus URLs,
// 'preview' tracks get only preview_mp3_128_url (a ~30s snippet). Each URL
// requires the OAuth header and 302s to a short-lived signed CDN URL.
public class SoundCloudStreams
{
    [JsonPropertyName("http_mp3_128_url")] public string? HttpMp3128Url { get; set; }
    [JsonPropertyName("hls_mp3_128_url")] public string? HlsMp3128Url { get; set; }
    [JsonPropertyName("hls_opus_64_url")] public string? HlsOpus64Url { get; set; }
    [JsonPropertyName("preview_mp3_128_url")] public string? PreviewMp3128Url { get; set; }
}

public class StreamResponse
{
    /// <summary>Signed CDN URL the browser can play directly. Expires within minutes.</summary>
    public string Url { get; set; } = "";
    /// <summary>True when only a ~30s preview is available to this account.</summary>
    public bool Preview { get; set; }
}
