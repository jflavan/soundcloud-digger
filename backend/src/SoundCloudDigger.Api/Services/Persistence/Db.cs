using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence;

public static class Db
{
    public static string DefaultFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "soundcloud-digger");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "app.db");
    }

    public static SqliteConnection Open(string? filePath = null)
    {
        filePath ??= DefaultFilePath();
        var conn = new SqliteConnection($"Data Source={filePath};Cache=Shared");
        conn.Open();
        ApplyPragmas(conn, walMode: true);
        TrySetOwnerOnlyPermissions(filePath);
        return conn;
    }

    public static SqliteConnection OpenInMemory()
    {
        // Each :memory: connection is a fresh DB; use a named shared memory DB
        // if multiple connections need to see the same state within one test.
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        ApplyPragmas(conn, walMode: false);
        return conn;
    }

    private static void ApplyPragmas(SqliteConnection conn, bool walMode)
    {
        using var cmd = conn.CreateCommand();
        if (walMode)
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
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
