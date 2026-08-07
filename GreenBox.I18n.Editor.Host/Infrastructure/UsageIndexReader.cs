using System.Globalization;
using GreenBox.I18n.Editor.Host.Contracts;
using Microsoft.Data.Sqlite;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Reads Unity's persisted usage index without taking ownership of its lifecycle.
/// </summary>
public sealed class UsageIndexReader
{
    private const string RelativeDatabasePath = "Library/GreenBox.I18n/usage-index.db";
    private readonly UnityProjectLocator _projectLocator;

    /// <summary>
    /// Creates a read-only usage-index reader.
    /// </summary>
    public UsageIndexReader(UnityProjectLocator projectLocator)
    {
        _projectLocator = projectLocator;
    }

    /// <summary>
    /// Reads index state and per-entry counts for the current project.
    /// </summary>
    public async Task<UsageIndexSummaryResponse> ReadSummaryAsync(
        string? catalogPath,
        CancellationToken cancellationToken)
    {
        UsageIndexLocation location = Locate(catalogPath);
        if (location.Availability != UsageIndexAvailability.Available)
        {
            return EmptySummary(location.Availability);
        }

        try
        {
            await using SqliteConnection connection = await OpenAsync(location.DatabasePath!, cancellationToken);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            (string status, DateTimeOffset? updatedAtUtc, string? lastError) =
                await ReadStateAsync(connection, transaction, cancellationToken);
            int failedSourceCount = await ReadFailedSourceCountAsync(connection, transaction, cancellationToken);
            IReadOnlyList<UsageEntrySummaryResponse> entries =
                await ReadEntrySummariesAsync(connection, transaction, cancellationToken);

            return new UsageIndexSummaryResponse(
                UsageIndexAvailability.Available,
                status,
                updatedAtUtc,
                lastError,
                failedSourceCount,
                entries);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return new UsageIndexSummaryResponse(
                UsageIndexAvailability.Error,
                null,
                null,
                exception.Message,
                0,
                Array.Empty<UsageEntrySummaryResponse>());
        }
    }

    /// <summary>
    /// Reads all indexed code and asset locations for one entry.
    /// </summary>
    public async Task<UsageEntryResponse> ReadEntryAsync(
        string? catalogPath,
        long entryId,
        CancellationToken cancellationToken)
    {
        string entryIdText = entryId.ToString(CultureInfo.InvariantCulture);
        UsageIndexLocation location = Locate(catalogPath);
        if (location.Availability != UsageIndexAvailability.Available)
        {
            return EmptyEntry(location.Availability, entryIdText);
        }

        try
        {
            await using SqliteConnection connection = await OpenAsync(location.DatabasePath!, cancellationToken);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            IReadOnlyList<CodeUsageResponse> code =
                await ReadCodeUsagesAsync(connection, transaction, entryId, cancellationToken);
            IReadOnlyList<AssetUsageResponse> assets =
                await ReadAssetUsagesAsync(connection, transaction, entryId, cancellationToken);
            return new UsageEntryResponse(
                UsageIndexAvailability.Available,
                entryIdText,
                code.Count + assets.Count,
                code,
                assets);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return EmptyEntry(UsageIndexAvailability.Error, entryIdText);
        }
    }

    private UsageIndexLocation Locate(string? catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return new UsageIndexLocation(UsageIndexAvailability.NoCatalog, null);
        }

        string? projectRoot = _projectLocator.FindProjectRoot(catalogPath);
        if (projectRoot == null)
        {
            return new UsageIndexLocation(UsageIndexAvailability.OutsideUnityProject, null);
        }

        string databasePath = Path.Combine(
            projectRoot,
            RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(databasePath)
            ? new UsageIndexLocation(UsageIndexAvailability.Available, databasePath)
            : new UsageIndexLocation(UsageIndexAvailability.NotCreated, databasePath);
    }

    private static async Task<SqliteConnection> OpenAsync(string databasePath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 2,
        };
        var connection = new SqliteConnection(connectionString.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<(string Status, DateTimeOffset? UpdatedAtUtc, string? LastError)> ReadStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT status, updated_at_utc, last_error FROM index_state WHERE id = 1;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("The usage index does not contain its state row.");
        }

        DateTimeOffset? updatedAtUtc = reader.IsDBNull(1)
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
        return (reader.GetString(0), updatedAtUtc, reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<int> ReadFailedSourceCountAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT COUNT(*) FROM sources WHERE status = 'failed';");
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<UsageEntrySummaryResponse>> ReadEntrySummariesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT entry_id, SUM(code_count), SUM(asset_count)
            FROM (
                SELECT usage.entry_id, COUNT(*) AS code_count, 0 AS asset_count
                FROM code_usages usage
                JOIN sources source ON source.source_id = usage.source_id
                WHERE source.status = 'current'
                GROUP BY usage.entry_id
                UNION ALL
                SELECT usage.entry_id, 0 AS code_count, COUNT(*) AS asset_count
                FROM asset_usages usage
                JOIN sources source ON source.source_id = usage.source_id
                WHERE source.status = 'current'
                GROUP BY usage.entry_id
            )
            GROUP BY entry_id
            ORDER BY entry_id;
            """;
        await using SqliteCommand command = CreateCommand(connection, transaction, sql);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var entries = new List<UsageEntrySummaryResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            int codeCount = reader.GetInt32(1);
            int assetCount = reader.GetInt32(2);
            entries.Add(new UsageEntrySummaryResponse(
                reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                codeCount + assetCount,
                codeCount,
                assetCount));
        }

        return entries;
    }

    private static async Task<IReadOnlyList<CodeUsageResponse>> ReadCodeUsagesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long entryId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT source.source_key, usage.file_path, usage.line
            FROM code_usages usage
            JOIN sources source ON source.source_id = usage.source_id
            WHERE usage.entry_id = @entry_id AND source.status = 'current'
            ORDER BY usage.file_path, usage.line;
            """;
        await using SqliteCommand command = CreateCommand(connection, transaction, sql);
        command.Parameters.AddWithValue("@entry_id", entryId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var usages = new List<CodeUsageResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            usages.Add(new CodeUsageResponse(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return usages;
    }

    private static async Task<IReadOnlyList<AssetUsageResponse>> ReadAssetUsagesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long entryId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT source.source_key, source.asset_guid,
                   usage.asset_local_id, usage.game_object_local_id,
                   usage.object_path, usage.component_type, usage.property_path, usage.line,
                   usage.is_prefab_override, usage.target_asset_guid, usage.target_local_id
            FROM asset_usages usage
            JOIN sources source ON source.source_id = usage.source_id
            WHERE usage.entry_id = @entry_id AND source.status = 'current'
            ORDER BY source.source_key, usage.object_path, usage.component_type, usage.property_path;
            """;
        await using SqliteCommand command = CreateCommand(connection, transaction, sql);
        command.Parameters.AddWithValue("@entry_id", entryId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var usages = new List<AssetUsageResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            usages.Add(new AssetUsageResponse(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetInt64(2).ToString(CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? null : reader.GetInt64(3).ToString(CultureInfo.InvariantCulture),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetBoolean(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetInt64(10).ToString(CultureInfo.InvariantCulture)));
        }

        return usages;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static bool IsReadFailure(Exception exception) =>
        exception is SqliteException or IOException or UnauthorizedAccessException or InvalidDataException;

    private static UsageIndexSummaryResponse EmptySummary(string availability) =>
        new(availability, null, null, null, 0, Array.Empty<UsageEntrySummaryResponse>());

    private static UsageEntryResponse EmptyEntry(string availability, string entryId) =>
        new(availability, entryId, 0, Array.Empty<CodeUsageResponse>(), Array.Empty<AssetUsageResponse>());

    private sealed record UsageIndexLocation(string Availability, string? DatabasePath);
}

/// <summary>
/// Stable usage-index availability values exposed to the Web editor.
/// </summary>
public static class UsageIndexAvailability
{
    public const string Available = "available";
    public const string NoCatalog = "noCatalog";
    public const string OutsideUnityProject = "outsideUnityProject";
    public const string NotCreated = "notCreated";
    public const string Error = "error";
}
