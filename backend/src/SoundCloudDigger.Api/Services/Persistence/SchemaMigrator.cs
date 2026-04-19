using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence;

public static class SchemaMigrator
{
    public static void Migrate(SqliteConnection conn, IEnumerable<IMigration> migrations)
    {
        var ordered = migrations.OrderBy(m => m.Version).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Version != i + 1)
                throw new InvalidOperationException(
                    $"Migrations must be sequential from 1. Got version {ordered[i].Version} at index {i}.");
        }

        var current = GetUserVersion(conn);
        foreach (var migration in ordered.Where(m => m.Version > current))
        {
            using var tx = conn.BeginTransaction();
            migration.Apply(conn);
            SetUserVersion(conn, migration.Version);
            tx.Commit();
        }
    }

    private static int GetUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version={version};";
        cmd.ExecuteNonQuery();
    }
}
