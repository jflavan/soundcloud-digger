namespace SoundCloudDigger.Api.Models;

public class FeedTrack
{
    public string Title { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string? ArtworkUrl { get; set; }
    public string? Genre { get; set; }
    public List<string> Tags { get; set; } = [];
    public int LikesCount { get; set; }
    public int PlaybackCount { get; set; }
    public int RepostsCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PermalinkUrl { get; set; }
    public int Duration { get; set; }
    public string? Access { get; set; }
    public string ActivityType { get; set; } = "";
    public DateTime AppearedAt { get; set; }

    public static FeedTrack FromActivity(SoundCloudActivity activity)
    {
        var track = activity.Origin;
        if (track is null) throw new ArgumentException("Activity has no origin track");

        return new FeedTrack
        {
            Title = track.Title,
            ArtistName = track.User?.Username ?? "",
            ArtworkUrl = track.ArtworkUrl,
            Genre = track.Genre,
            Tags = ParseTagList(track.TagList),
            LikesCount = track.FavoritingsCount ?? 0,
            PlaybackCount = track.PlaybackCount ?? 0,
            RepostsCount = track.RepostsCount ?? 0,
            CommentCount = track.CommentCount ?? 0,
            CreatedAt = track.CreatedAt,
            PermalinkUrl = track.PermalinkUrl,
            Duration = track.Duration ?? 0,
            Access = track.Access,
            ActivityType = activity.Type,
            AppearedAt = activity.CreatedAt,
        };
    }

    private static List<string> ParseTagList(string? tagList)
    {
        if (string.IsNullOrWhiteSpace(tagList)) return [];

        var tags = new List<string>();
        var span = tagList.AsSpan();
        var inQuote = false;
        var start = 0;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || (span[i] == ' ' && !inQuote))
            {
                if (i > start)
                {
                    var tag = span[start..i].Trim('"').ToString().Trim();
                    if (tag.Length > 0) tags.Add(tag);
                }
                start = i + 1;
            }
            else if (span[i] == '"')
            {
                inQuote = !inQuote;
            }
        }

        return tags;
    }
}
