using Dapper;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;
using System.Text.Json;

namespace SoundCloudDigger.Tests.Services;

public class DiscoverRepositoryTests
{
    private Microsoft.Data.Sqlite.SqliteConnection Seed()
    {
        var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });

        void AddArtist(string urn, string name) =>
            conn.Execute(
                "INSERT INTO users (urn, username, fetched_at) VALUES (@urn, @name, 0);",
                new { urn, name });

        void AddTrack(string urn, string title) =>
            conn.Execute(
                "INSERT INTO tracks (urn, payload_json, updated_at) VALUES (@urn, @payload, 0);",
                new { urn, payload = JsonSerializer.Serialize(new FeedTrack { PermalinkUrl = urn, Title = title }) });

        void AddRepost(string artist, string track, long ts) =>
            conn.Execute(
                "INSERT INTO artist_reposts (artist_urn, track_urn, reposted_at) VALUES (@a, @t, @ts);",
                new { a = artist, t = track, ts });

        AddArtist("a1", "Alice"); AddArtist("a2", "Bob"); AddArtist("a3", "Carol");
        AddTrack("trackA", "A"); AddTrack("trackB", "B");

        // 3 reposts of A, 1 of B
        AddRepost("a1", "trackA", 100);
        AddRepost("a2", "trackA", 200);
        AddRepost("a3", "trackA", 150);
        AddRepost("a1", "trackB", 50);

        // user u1 follows a1, a2, a3
        foreach (var u in new[] { "a1", "a2", "a3" })
            conn.Execute(
                "INSERT INTO followings (user_urn, followed_urn, fetched_at) VALUES ('u1', @u, 0);",
                new { u });

        return conn;
    }

    [Fact]
    public void GetConsensus_RanksByReposterCountDesc()
    {
        using var conn = Seed();
        var repo = new DiscoverRepository(conn);

        var result = repo.GetConsensus("u1");

        Assert.Equal(2, result.Count);
        Assert.Equal("trackA", result[0].PermalinkUrl);
        Assert.Equal(3, result[0].ReposterCount);
        Assert.Equal("trackB", result[1].PermalinkUrl);
        Assert.Equal(1, result[1].ReposterCount);
    }

    [Fact]
    public void GetConsensus_IncludesReposters()
    {
        using var conn = Seed();
        var repo = new DiscoverRepository(conn);

        var result = repo.GetConsensus("u1");
        var a = result.First(t => t.PermalinkUrl == "trackA");

        Assert.Equal(3, a.Reposters.Length);
        Assert.Contains("Alice", a.Reposters);
    }

    [Fact]
    public void GetProgress_ReturnsFractionFetched()
    {
        using var conn = Seed();
        conn.Execute(
            "INSERT INTO artist_fetch_state (artist_urn, last_fetched_at) VALUES ('a1', 0), ('a2', 0);");
        var repo = new DiscoverRepository(conn);

        var (fetched, total) = repo.GetProgress("u1");

        Assert.Equal(2, fetched);
        Assert.Equal(3, total);
    }
}
