using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Tests.Services.Persistence;

public class SchemaMigratorTests
{
    private class NoopMigration : IMigration
    {
        public int Version { get; }
        public Action<SqliteConnection> Apply { get; }
        public NoopMigration(int version, Action<SqliteConnection> apply)
        {
            Version = version;
            Apply = apply;
        }
        void IMigration.Apply(SqliteConnection conn) => Apply(conn);
    }

    [Fact]
    public void Migrate_AppliesMigrationsInOrder()
    {
        using var conn = Db.OpenInMemory();
        var applied = new List<int>();
        var migrations = new IMigration[]
        {
            new NoopMigration(1, _ => applied.Add(1)),
            new NoopMigration(2, _ => applied.Add(2)),
        };

        SchemaMigrator.Migrate(conn, migrations);

        Assert.Equal(new[] { 1, 2 }, applied);
    }

    [Fact]
    public void Migrate_SkipsAlreadyAppliedMigrations()
    {
        using var conn = Db.OpenInMemory();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version=1;";
            cmd.ExecuteNonQuery();
        }
        var applied = new List<int>();
        var migrations = new IMigration[]
        {
            new NoopMigration(1, _ => applied.Add(1)),
            new NoopMigration(2, _ => applied.Add(2)),
        };

        SchemaMigrator.Migrate(conn, migrations);

        Assert.Equal(new[] { 2 }, applied);
    }

    [Fact]
    public void Migrate_UpdatesUserVersion()
    {
        using var conn = Db.OpenInMemory();
        var migrations = new IMigration[] { new NoopMigration(1, _ => { }) };

        SchemaMigrator.Migrate(conn, migrations);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(1, version);
    }

    [Fact]
    public void Migrate_ThrowsOnNonSequentialVersions()
    {
        using var conn = Db.OpenInMemory();
        var migrations = new IMigration[]
        {
            new NoopMigration(1, _ => { }),
            new NoopMigration(3, _ => { }),
        };

        Assert.Throws<InvalidOperationException>(
            () => SchemaMigrator.Migrate(conn, migrations));
    }
}
