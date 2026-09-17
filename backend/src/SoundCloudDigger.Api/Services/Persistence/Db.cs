using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence;

// Connection factory. Every DB operation opens its own short-lived connection via
// Open(); Microsoft.Data.Sqlite pools them, and SQLite's WAL mode lets readers run
// concurrently with a single writer (writers queue on busy_timeout). This replaces
// the old single shared SqliteConnection + process-wide lock.
public sealed class Db : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;

    private Db(string connectionString, SqliteConnection? keepAlive)
    {
        _connectionString = connectionString;
        _keepAlive = keepAlive;
    }

    public static string DefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "soundcloud-digger");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "app.db");
    }

    public static Db ForFile(string? filePath = null)
    {
        filePath ??= DefaultFilePath();
        var db = new Db($"Data Source={filePath}", keepAlive: null);
        // journal_mode is persistent in the file, so set it once here rather than per open.
        using (var conn = db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        TrySetOwnerOnlyPermissions(filePath);
        return db;
    }

    // A named shared-cache in-memory database. It lives as long as at least one
    // connection is open, so we hold one for the lifetime of this Db.
    public static Db InMemory()
    {
        var name = $"memdb-{Guid.NewGuid():N}";
        var cs = $"Data Source=file:{name}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(cs);
        keepAlive.Open();
        return new Db(cs, keepAlive);
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        // Per-connection pragmas; cheap, and pooled connections don't guarantee state.
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public void Dispose()
    {
        _keepAlive?.Dispose();
        // Drop pooled connections so an in-memory DB is released and a file DB is closed.
        SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
    }

    private static void TrySetOwnerOnlyPermissions(string filePath)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best effort; don't fail startup if the FS doesn't support it.
        }
    }
}
