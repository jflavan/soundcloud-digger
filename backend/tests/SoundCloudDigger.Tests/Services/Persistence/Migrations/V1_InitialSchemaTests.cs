using Dapper;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests.Services.Persistence.Migrations;

public class V1_InitialSchemaTests
{
    [Theory]
    [InlineData("sessions")]
    [InlineData("users")]
    [InlineData("tracks")]
    [InlineData("feed_tracks")]
    [InlineData("followings")]
    [InlineData("artist_reposts")]
    [InlineData("artist_fetch_state")]
    [InlineData("user_fetch_state")]
    public void Creates_ExpectedTables(string tableName)
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });

        var found = conn.ExecuteScalar<string?>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name=@name;",
            new { name = tableName });

        Assert.Equal(tableName, found);
    }

    [Fact]
    public void Creates_ExpectedIndexes()
    {
        using var conn = Db.OpenInMemory();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });

        var indexes = conn.Query<string>(
            "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%';")
            .ToHashSet();

        Assert.Contains("idx_sessions_user", indexes);
        Assert.Contains("idx_feed_tracks_user_appeared", indexes);
        Assert.Contains("idx_artist_reposts_artist", indexes);
    }
}
