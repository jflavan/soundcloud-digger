using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence.Migrations;

public class V2_ArtistFullResetAt : IMigration
{
    public int Version => 2;

    public void Apply(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
ALTER TABLE artist_fetch_state ADD COLUMN last_full_reset_at INTEGER NOT NULL DEFAULT 0;
";
        cmd.ExecuteNonQuery();
    }
}
