#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;

namespace GreenBox.I18n.Usage.Index
{
    /// <summary>
    /// Replaces source-owned usage rows inside an existing index transaction.
    /// </summary>
    internal static class I18nUsageIndexSourceWriter
    {
        internal const string AssetKind = "asset";
        internal const string AssemblyKind = "assembly";

        internal static void ApplyAssets(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            IEnumerable<I18nAssetUsageSourceScanResult> sources)
        {
            foreach (I18nAssetUsageSourceScanResult source in sources)
            {
                switch (source.Status)
                {
                    case I18nUsageSourceScanStatus.Success:
                    {
                        long sourceId = UpsertSource(
                            connection,
                            transaction,
                            AssetKind,
                            source.SourceKey,
                            NullIfEmpty(source.AssetGuid),
                            "current",
                            null);
                        DeleteUsages(connection, "asset_usages", sourceId);
                        InsertAssetUsages(connection, sourceId, source.Usages);
                        break;
                    }
                    case I18nUsageSourceScanStatus.Failed:
                        UpsertSource(
                            connection,
                            transaction,
                            AssetKind,
                            source.SourceKey,
                            NullIfEmpty(source.AssetGuid),
                            "failed",
                            source.Error ?? "Asset analysis failed.");
                        break;
                    case I18nUsageSourceScanStatus.Changed:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(source.Status), source.Status, null);
                }
            }
        }

        internal static void ApplyAssemblies(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            IEnumerable<I18nIlUsageSourceScanResult> sources)
        {
            foreach (I18nIlUsageSourceScanResult source in sources)
            {
                switch (source.Status)
                {
                    case I18nUsageSourceScanStatus.Success:
                    {
                        long sourceId = UpsertSource(
                            connection,
                            transaction,
                            AssemblyKind,
                            source.SourceKey,
                            null,
                            "current",
                            null);
                        DeleteUsages(connection, "code_usages", sourceId);
                        InsertCodeUsages(connection, sourceId, source.Usages);
                        break;
                    }
                    case I18nUsageSourceScanStatus.Failed:
                        UpsertSource(
                            connection,
                            transaction,
                            AssemblyKind,
                            source.SourceKey,
                            null,
                            "failed",
                            source.Error ?? "IL analysis failed.");
                        break;
                    case I18nUsageSourceScanStatus.Changed:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(source.Status), source.Status, null);
                }
            }
        }

        internal static void ReconcileSources(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            string kind,
            IEnumerable<string> sourceKeys)
        {
            connection.Execute(@"
CREATE TEMP TABLE IF NOT EXISTS scan_source_keys (
    kind       TEXT NOT NULL,
    source_key TEXT NOT NULL,
    PRIMARY KEY(kind, source_key)
) WITHOUT ROWID;
DELETE FROM scan_source_keys;");
            using (I18nSqliteStatement insert = connection.Prepare(@"
INSERT OR IGNORE INTO scan_source_keys(kind, source_key)
VALUES (@kind, @source_key);"))
            {
                foreach (string sourceKey in NormalizeSourceKeys(sourceKeys))
                {
                    insert.Bind("@kind", kind).Bind("@source_key", sourceKey);
                    insert.ExecuteNonQuery();
                }
            }

            using I18nSqliteStatement delete = connection.Prepare(@"
DELETE FROM sources
WHERE kind = @kind
  AND NOT EXISTS (
      SELECT 1
      FROM scan_source_keys scanned
      WHERE scanned.kind = sources.kind
        AND scanned.source_key = sources.source_key
  );");
            delete.Bind("@kind", kind);
            delete.ExecuteNonQuery();
        }

        internal static long UpsertSource(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            string kind,
            string sourceKey,
            string? assetGuid,
            string status,
            string? error)
        {
            using (I18nSqliteStatement insert = connection.Prepare(@"
INSERT OR IGNORE INTO sources(kind, source_key, asset_guid, status, error)
VALUES (@kind, @source_key, @asset_guid, @status, @error);"))
            {
                AddSourceParameters(insert, kind, sourceKey, assetGuid, status, error);
                insert.ExecuteNonQuery();
            }

            using (I18nSqliteStatement update = connection.Prepare(@"
UPDATE sources
SET asset_guid = COALESCE(@asset_guid, asset_guid),
    status = @status,
    error = @error
WHERE kind = @kind AND source_key = @source_key;"))
            {
                AddSourceParameters(update, kind, sourceKey, assetGuid, status, error);
                update.ExecuteNonQuery();
            }

            using I18nSqliteStatement select = connection.Prepare(@"
SELECT source_id
FROM sources
WHERE kind = @kind AND source_key = @source_key;");
            select.Bind("@kind", kind).Bind("@source_key", sourceKey);
            return select.ExecuteScalarInt64();
        }

        internal static IEnumerable<string> NormalizeSourceKeys(IEnumerable<string> sourceKeys)
        {
            return sourceKeys
                .Where(sourceKey => !string.IsNullOrWhiteSpace(sourceKey))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void InsertCodeUsages(
            I18nSqliteConnection connection,
            long sourceId,
            IEnumerable<I18nIlUsage> usages)
        {
            using I18nSqliteStatement command = connection.Prepare(@"
INSERT OR IGNORE INTO code_usages(source_id, entry_id, file_path, line)
VALUES (@source_id, @entry_id, @file_path, @line);");
            foreach (I18nIlUsage usage in usages)
            {
                command
                    .Bind("@source_id", sourceId)
                    .Bind("@entry_id", usage.EntryId)
                    .Bind("@file_path", usage.AssetPath)
                    .Bind("@line", usage.Line);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertAssetUsages(
            I18nSqliteConnection connection,
            long sourceId,
            IEnumerable<I18nAssetUsage> usages)
        {
            using I18nSqliteStatement command = connection.Prepare(@"
INSERT INTO asset_usages(
    source_id, entry_id, asset_local_id, game_object_local_id, object_path,
    component_type, property_path, line, is_prefab_override,
    target_asset_guid, target_local_id)
VALUES (
    @source_id, @entry_id, @asset_local_id, @game_object_local_id, @object_path,
    @component_type, @property_path, @line, @is_prefab_override,
    @target_asset_guid, @target_local_id);");
            foreach (I18nAssetUsage usage in usages)
            {
                command
                    .Bind("@source_id", sourceId)
                    .Bind("@entry_id", usage.EntryId)
                    .Bind("@asset_local_id", usage.AssetLocalId);
                BindOptionalLong(command, "@game_object_local_id", usage.GameObjectLocalId);
                command
                    .Bind("@object_path", usage.ObjectPath)
                    .Bind("@component_type", usage.ComponentType)
                    .Bind("@property_path", usage.PropertyPath)
                    .Bind("@line", usage.Line)
                    .Bind("@is_prefab_override", usage.IsPrefabOverride ? 1 : 0)
                    .Bind("@target_asset_guid", NullIfEmpty(usage.TargetAssetGuid));
                BindOptionalLong(command, "@target_local_id", usage.TargetLocalId);
                command.ExecuteNonQuery();
            }
        }

        private static void AddSourceParameters(
            I18nSqliteStatement command,
            string kind,
            string sourceKey,
            string? assetGuid,
            string status,
            string? error)
        {
            command
                .Bind("@kind", kind)
                .Bind("@source_key", sourceKey)
                .Bind("@asset_guid", assetGuid)
                .Bind("@status", status)
                .Bind("@error", error);
        }

        private static void DeleteUsages(
            I18nSqliteConnection connection,
            string table,
            long sourceId)
        {
            if (table != "asset_usages" && table != "code_usages")
            {
                throw new ArgumentOutOfRangeException(nameof(table));
            }

            using I18nSqliteStatement command = connection.Prepare(
                $"DELETE FROM {table} WHERE source_id = @source_id;");
            command.Bind("@source_id", sourceId);
            command.ExecuteNonQuery();
        }

        private static string? NullIfEmpty(string value)
        {
            return value.Length == 0 ? null : value;
        }

        private static void BindOptionalLong(
            I18nSqliteStatement statement,
            string parameterName,
            long value)
        {
            if (value == 0)
            {
                statement.BindNull(parameterName);
            }
            else
            {
                statement.Bind(parameterName, value);
            }
        }
    }
}
