using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

public class SoundCloudDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] Formats =
    [
        "yyyy/MM/dd HH:mm:ss zzz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy/MM/dd HH:mm:ss '+0000'",
    ];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null) return default;

        // SoundCloud uses "2026/03/13 21:22:58 +0000" format
        if (DateTimeOffset.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.UtcDateTime;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
            return dto.UtcDateTime;

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}

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
    [JsonConverter(typeof(SoundCloudDateTimeConverter))]
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
    public int? FavoritingsCount { get; set; }

    [JsonPropertyName("playback_count")]
    public int? PlaybackCount { get; set; }

    [JsonPropertyName("reposts_count")]
    public int? RepostsCount { get; set; }

    [JsonPropertyName("comment_count")]
    public int? CommentCount { get; set; }

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(SoundCloudDateTimeConverter))]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("permalink_url")]
    public string? PermalinkUrl { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("access")]
    public string? Access { get; set; }

    [JsonPropertyName("user")]
    public SoundCloudUser? User { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public class SoundCloudUser
{
    [JsonPropertyName("urn")]
    public string Urn { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("permalink_url")]
    public string PermalinkUrl { get; set; } = "";
}

public class SoundCloudUsersResponse
{
    [JsonPropertyName("collection")]
    public List<SoundCloudUser> Collection { get; set; } = new();

    [JsonPropertyName("next_href")]
    public string? NextHref { get; set; }
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
