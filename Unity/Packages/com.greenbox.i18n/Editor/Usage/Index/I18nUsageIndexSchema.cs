#nullable enable

using System;

namespace GreenBox.I18n.Usage.Index
{
    /// <summary>
    /// Owns the on-disk SQLite schema and forward-only migrations for the usage index.
    /// </summary>
    internal static class I18nUsageIndexSchema
    {
        internal const int CurrentVersion = 1;

        internal static void EnsureCreated(I18nSqliteConnection connection)
        {
            int version = Convert.ToInt32(connection.ExecuteScalarInt64("PRAGMA user_version;"));
            if (version > CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Usage index schema version {version} is newer than supported version {CurrentVersion}.");
            }

            if (version == 0)
            {
                CreateVersionOne(connection);
                version = 1;
            }

            if (version != CurrentVersion)
            {
                throw new InvalidOperationException(
                    $"Usage index schema migration stopped at version {version}; " +
                    $"expected {CurrentVersion}.");
            }
        }

        private static void CreateVersionOne(I18nSqliteConnection connection)
        {
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            connection.Execute(@"
CREATE TABLE index_state (
    id             INTEGER PRIMARY KEY CHECK (id = 1),
    status         TEXT NOT NULL CHECK (status IN ('empty', 'ready', 'updating', 'disabled', 'error')),
    updated_at_utc INTEGER,
    last_error     TEXT
);

CREATE TABLE sources (
    source_id  INTEGER PRIMARY KEY,
    kind       TEXT NOT NULL CHECK (kind IN ('asset', 'assembly')),
    source_key TEXT NOT NULL,
    asset_guid TEXT,
    status     TEXT NOT NULL CHECK (status IN ('current', 'pending', 'failed')),
    error      TEXT,

    UNIQUE(kind, source_key)
);

CREATE INDEX sources_asset_guid
ON sources(asset_guid)
WHERE asset_guid IS NOT NULL;

CREATE TABLE code_usages (
    source_id INTEGER NOT NULL
        REFERENCES sources(source_id) ON DELETE CASCADE,
    entry_id  INTEGER NOT NULL,
    file_path TEXT NOT NULL,
    line      INTEGER NOT NULL,

    PRIMARY KEY(source_id, entry_id, file_path, line)
);

CREATE INDEX code_usages_entry_id
ON code_usages(entry_id);

CREATE TABLE asset_usages (
    usage_id             INTEGER PRIMARY KEY,
    source_id            INTEGER NOT NULL
        REFERENCES sources(source_id) ON DELETE CASCADE,
    entry_id             INTEGER NOT NULL,
    asset_local_id       INTEGER NOT NULL,
    game_object_local_id INTEGER,
    object_path          TEXT NOT NULL,
    component_type       TEXT NOT NULL,
    property_path        TEXT NOT NULL,
    line                 INTEGER NOT NULL,
    is_prefab_override   INTEGER NOT NULL CHECK (is_prefab_override IN (0, 1)),
    target_asset_guid    TEXT,
    target_local_id      INTEGER
);

CREATE INDEX asset_usages_entry_id
ON asset_usages(entry_id);

CREATE INDEX asset_usages_source_id
ON asset_usages(source_id);

INSERT INTO index_state(id, status)
VALUES (1, 'empty');

PRAGMA user_version = 1;
");
            transaction.Commit();
        }
    }
}
