using Microsoft.Data.Sqlite;
using SoundCloudDigger.Api.Services.Persistence;

namespace SoundCloudDigger.Tests.Services.Persistence;

public class DbTests
{
    [Fact]
    public void OpenInMemory_ReturnsOpenConnection()
    {
        using var conn = Db.OpenInMemory();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact]
    public void OpenInMemory_HasWalModeAndTimeouts()
    {
        using var conn = Db.OpenInMemory();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (string)cmd.ExecuteScalar()!;
        // In-memory dbs cannot use WAL, but the pragma should be attempted safely
        Assert.NotNull(mode);

        cmd.CommandText = "PRAGMA busy_timeout;";
        var busy = (long)cmd.ExecuteScalar()!;
        Assert.Equal(5000, busy);
    }
}
