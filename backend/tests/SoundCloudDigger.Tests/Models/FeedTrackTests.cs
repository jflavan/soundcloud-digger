using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Tests.Models;

public class FeedTrackTests
{
    private static SoundCloudTrack MakeTrack(string? tagList = null) => new()
    {
        Title = "Song",
        ArtworkUrl = "https://img/a.jpg",
        Genre = "Techno",
        TagList = tagList,
        FavoritingsCount = 5,
        PlaybackCount = 50,
        RepostsCount = 2,
        CommentCount = 1,
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        PermalinkUrl = "https://soundcloud.com/a/song",
        Duration = 180_000,
        Access = "playable",
        User = new SoundCloudUser { Username = "artist" },
    };

    [Fact]
    public void FromActivity_MapsAllFields()
    {
        var appeared = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc);
        var activity = new SoundCloudActivity { Type = "track-repost", CreatedAt = appeared, Origin = MakeTrack("techno house") };

        var t = FeedTrack.FromActivity(activity);

        Assert.Equal("Song", t.Title);
        Assert.Equal("artist", t.ArtistName);
        Assert.Equal("Techno", t.Genre);
        Assert.Equal(["techno", "house"], t.Tags);
        Assert.Equal(5, t.LikesCount);
        Assert.Equal(50, t.PlaybackCount);
        Assert.Equal(2, t.RepostsCount);
        Assert.Equal(1, t.CommentCount);
        Assert.Equal(180_000, t.Duration);
        Assert.Equal("track-repost", t.ActivityType);
        Assert.Equal(appeared, t.AppearedAt);
    }

    [Fact]
    public void FromActivity_ThrowsWhenOriginMissing()
    {
        var activity = new SoundCloudActivity { Type = "track", Origin = null };
        Assert.Throws<ArgumentException>(() => FeedTrack.FromActivity(activity));
    }

    [Fact]
    public void FromActivity_DefaultsNullCountsToZeroAndMissingUserToEmpty()
    {
        var track = MakeTrack();
        track.FavoritingsCount = null; track.PlaybackCount = null; track.RepostsCount = null;
        track.CommentCount = null; track.Duration = null; track.User = null;

        var t = FeedTrack.FromActivity(new SoundCloudActivity { Origin = track });

        Assert.Equal(0, t.LikesCount);
        Assert.Equal(0, t.PlaybackCount);
        Assert.Equal(0, t.RepostsCount);
        Assert.Equal(0, t.CommentCount);
        Assert.Equal(0, t.Duration);
        Assert.Equal("", t.ArtistName);
    }

    [Fact]
    public void FromTrack_MapsFieldsAndAppearedAt()
    {
        var appeared = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc);
        var t = FeedTrack.FromTrack(MakeTrack("\"deep house\" ambient"), appeared);

        Assert.Equal("Song", t.Title);
        Assert.Equal("track-repost", t.ActivityType);
        Assert.Equal(["deep house", "ambient"], t.Tags);
        Assert.Equal(appeared, t.AppearedAt);
    }

    [Fact]
    public void FromTrack_UsesEmptyStringFallbacks()
    {
        var track = new SoundCloudTrack { Title = null!, CreatedAt = DateTime.UtcNow };

        var t = FeedTrack.FromTrack(track, track.CreatedAt);

        Assert.Equal("", t.Title);
        Assert.Equal("", t.ArtistName);
        Assert.Equal("", t.ArtworkUrl);
        Assert.Equal("", t.Genre);
        Assert.Equal("", t.PermalinkUrl);
        Assert.Equal("playable", t.Access);
        Assert.Empty(t.Tags);
        Assert.Equal(track.CreatedAt, t.AppearedAt);
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("techno", new[] { "techno" })]
    [InlineData("techno  house", new[] { "techno", "house" })]
    [InlineData("\"deep house\" techno \"melodic techno\"", new[] { "deep house", "techno", "melodic techno" })]
    [InlineData("\"\" techno", new[] { "techno" })]
    public void ParseTagList_HandlesQuotedAndWhitespaceCases(string? tagList, string[] expected)
    {
        var t = FeedTrack.FromActivity(new SoundCloudActivity { Origin = MakeTrack(tagList) });
        Assert.Equal(expected, t.Tags);
    }
}
