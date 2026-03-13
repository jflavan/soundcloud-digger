using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

public class SoundCloudActivitiesResponse
{
    [JsonPropertyName("collection")]
    public List<SoundCloudActivity> Collection { get; set; } = [];

    [JsonPropertyName("next_href")]
    public string? NextHref { get; set; }

    [JsonPropertyName("future_href")]
    public string? FutureHref { get; set; }
}

public class SoundCloudActivity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("origin")]
    public SoundCloudTrack? Origin { get; set; }
}

public class SoundCloudTrack
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artwork_url")]
    public string? ArtworkUrl { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("tag_list")]
    public string? TagList { get; set; }

    [JsonPropertyName("favoritings_count")]
    public int FavoritingsCount { get; set; }

    [JsonPropertyName("playback_count")]
    public int PlaybackCount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("permalink_url")]
    public string? PermalinkUrl { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("access")]
    public string? Access { get; set; }

    [JsonPropertyName("user")]
    public SoundCloudUser? User { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public class SoundCloudUser
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public class SoundCloudTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
