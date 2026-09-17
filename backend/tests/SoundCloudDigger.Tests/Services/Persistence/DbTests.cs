using Dapper;
using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Tests.Services.Persistence;

public class DbTests
{
    [Fact]
    public void InMemory_Open_ReturnsOpenConnection()
    {
        using var db = Db.InMemory();
        using var conn = db.Open();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact]
    public void Open_AppliesPerConnectionPragmas()
    {
        using var db = Db.InMemory();
        using var conn = db.Open();
        Assert.Equal(5000L, conn.ExecuteScalar<long>("PRAGMA busy_timeout;"));
        Assert.Equal(1L, conn.ExecuteScalar<long>("PRAGMA foreign_keys;"));
    }

    [Fact]
    public void InMemory_SeparateConnectionsSeeTheSameDatabase()
    {
        using var db = Db.InMemory();
        using (var writer = db.Open())
        {
            writer.Execute("CREATE TABLE t (x INTEGER); INSERT INTO t VALUES (1), (2);");
        }
        using var reader = db.Open();
        Assert.Equal(2L, reader.ExecuteScalar<long>("SELECT COUNT(*) FROM t;"));
    }

    [Fact]
    public void InMemory_InstancesAreIsolatedFromEachOther()
    {
        using var a = Db.InMemory();
        using var b = Db.InMemory();
        using (var conn = a.Open()) conn.Execute("CREATE TABLE only_in_a (x INTEGER);");
        using var other = b.Open();
        Assert.Equal(0L, other.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='only_in_a';"));
    }

    [Fact]
    public void ForFile_EnablesWalJournalMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scdigger-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = Db.ForFile(path))
            using (var conn = db.Open())
            {
                Assert.Equal("wal", conn.ExecuteScalar<string>("PRAGMA journal_mode;"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                try { File.Delete(path + suffix); } catch { }
        }
    }
}
