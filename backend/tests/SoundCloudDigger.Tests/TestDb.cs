using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

namespace SoundCloudDigger.Tests;

public static class TestDb
{
    // A fresh, fully migrated in-memory database. Dispose it to drop the data.
    public static Db Create()
    {
        var db = Db.InMemory();
        using var conn = db.Open();
        SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema(), new V2_ArtistFullResetAt() });
        return db;
    }
}
