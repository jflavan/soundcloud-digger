using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence;

public interface IMigration
{
    int Version { get; }
    void Apply(SqliteConnection conn);
}
