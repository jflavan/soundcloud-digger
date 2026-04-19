using Microsoft.Data.Sqlite;

namespace SoundCloudDigger.Api.Services.Persistence.Migrations;

public class V1_InitialSchema : IMigration
{
    public int Version => 1;

    public void Apply(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE sessions (
    session_id TEXT PRIMARY KEY,
    user_urn TEXT NOT NULL,
    access_token TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    token_expires_at INTEGER NOT NULL,
    created_at INTEGER NOT NULL,
    last_seen_at INTEGER NOT NULL
);
CREATE INDEX idx_sessions_user ON sessions(user_urn);

CREATE TABLE users (
    urn TEXT PRIMARY KEY,
    username TEXT,
    display_name TEXT,
    fetched_at INTEGER
);

CREATE TABLE tracks (
    urn TEXT PRIMARY KEY,
    payload_json TEXT NOT NULL,
    updated_at INTEGER NOT NULL
);

CREATE TABLE feed_tracks (
    user_urn TEXT NOT NULL,
    track_urn TEXT NOT NULL,
    appeared_at INTEGER NOT NULL,
    activity_type TEXT NOT NULL,
    PRIMARY KEY (user_urn, track_urn)
);
CREATE INDEX idx_feed_tracks_user_appeared ON feed_tracks(user_urn, appeared_at DESC);

CREATE TABLE followings (
    user_urn TEXT NOT NULL,
    followed_urn TEXT NOT NULL,
    fetched_at INTEGER NOT NULL,
    PRIMARY KEY (user_urn, followed_urn)
);

CREATE TABLE artist_reposts (
    artist_urn TEXT NOT NULL,
    track_urn TEXT NOT NULL,
    reposted_at INTEGER NOT NULL,
    PRIMARY KEY (artist_urn, track_urn)
);
CREATE INDEX idx_artist_reposts_artist ON artist_reposts(artist_urn);

CREATE TABLE artist_fetch_state (
    artist_urn TEXT PRIMARY KEY,
    cursor TEXT,
    last_fetched_at INTEGER NOT NULL
);

CREATE TABLE user_fetch_state (
    user_urn TEXT PRIMARY KEY,
    feed_last_fetched_at INTEGER,
    discover_last_fetched_at INTEGER
);
";
        cmd.ExecuteNonQuery();
    }
}
