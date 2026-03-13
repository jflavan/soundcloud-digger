using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class FeedCacheTests
{
    private readonly FeedCache _sut = new();

    private static FeedTrack MakeTrack(string title, DateTime createdAt, int likes = 0, string? genre = null)
    {
        return new FeedTrack
        {
            Title = title,
            ArtistName = "artist",
            CreatedAt = createdAt,
            LikesCount = likes,
            Genre = genre,
            AppearedAt = createdAt,
        };
    }

    [Fact]
    public void GetTracks_EmptyCache_ReturnsEmptyList()
    {
        var result = _sut.GetTracks("user1");
        Assert.Empty(result);
    }

    [Fact]
    public void AddTracks_AndRetrieve_ReturnsTracks()
    {
        var tracks = new List<FeedTrack>
        {
            MakeTrack("Track A", DateTime.UtcNow),
            MakeTrack("Track B", DateTime.UtcNow),
        };
        _sut.AddTracks("user1", tracks);
        var result = _sut.GetTracks("user1");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AddTracks_MultipleBatches_Accumulates()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.AddTracks("user1", [MakeTrack("B", DateTime.UtcNow)]);
        Assert.Equal(2, _sut.GetTracks("user1").Count);
    }

    [Fact]
    public void GetTracks_SeparateUsers_AreIsolated()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.AddTracks("user2", [MakeTrack("B", DateTime.UtcNow)]);
        Assert.Single(_sut.GetTracks("user1"));
        Assert.Single(_sut.GetTracks("user2"));
    }

    [Fact]
    public void SetLoadingComplete_ReflectsState()
    {
        Assert.False(_sut.IsLoadingComplete("user1"));
        _sut.SetLoadingComplete("user1", true);
        Assert.True(_sut.IsLoadingComplete("user1"));
    }

    [Fact]
    public void Clear_RemovesUserData()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.SetLoadingComplete("user1", true);
        _sut.Clear("user1");
        Assert.Empty(_sut.GetTracks("user1"));
        Assert.False(_sut.IsLoadingComplete("user1"));
    }
}
