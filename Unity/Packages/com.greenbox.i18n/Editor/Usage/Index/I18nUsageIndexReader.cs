#nullable enable

using System;
using System.Collections.Generic;

namespace GreenBox.I18n.Usage.Index
{
    internal enum I18nIndexedUsageKind
    {
        Code,
        Asset,
    }

    internal sealed class I18nIndexedUsage
    {
        public I18nIndexedUsage(
            long entryId,
            I18nIndexedUsageKind kind,
            string sourceKey,
            string filePath,
            int line,
            string objectPath,
            string componentType,
            string propertyPath)
        {
            EntryId = entryId;
            Kind = kind;
            SourceKey = sourceKey;
            FilePath = filePath;
            Line = line;
            ObjectPath = objectPath;
            ComponentType = componentType;
            PropertyPath = propertyPath;
        }

        public long EntryId { get; }
        public I18nIndexedUsageKind Kind { get; }
        public string SourceKey { get; }
        public string FilePath { get; }
        public int Line { get; }
        public string ObjectPath { get; }
        public string ComponentType { get; }
        public string PropertyPath { get; }
    }

    internal sealed class I18nUsageIndexSnapshot
    {
        public I18nUsageIndexSnapshot(string status, IReadOnlyList<I18nIndexedUsage> usages)
        {
            Status = status;
            Usages = usages;
        }

        public string Status { get; }
        public IReadOnlyList<I18nIndexedUsage> Usages { get; }
        public bool IsReady => string.Equals(Status, "ready", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads immutable snapshots without exposing SQLite details to index consumers.
    /// </summary>
    internal static class I18nUsageIndexReader
    {
        internal static I18nUsageIndexSnapshot ReadCurrent(I18nSqliteConnection connection)
        {
            string status = connection.ExecuteScalarString(@"
SELECT status
FROM index_state
WHERE id = 1;");
            var usages = new List<I18nIndexedUsage>();
            using I18nSqliteStatement statement = connection.Prepare(@"
SELECT
    code.entry_id,
    0 AS usage_kind,
    source.source_key,
    code.file_path,
    code.line,
    '' AS object_path,
    '' AS component_type,
    '' AS property_path
FROM code_usages code
INNER JOIN sources source ON source.source_id = code.source_id
WHERE source.status = 'current'

UNION ALL

SELECT
    asset.entry_id,
    1 AS usage_kind,
    source.source_key,
    source.source_key AS file_path,
    asset.line,
    asset.object_path,
    asset.component_type,
    asset.property_path
FROM asset_usages asset
INNER JOIN sources source ON source.source_id = asset.source_id
WHERE source.status = 'current'

ORDER BY entry_id, usage_kind, file_path, line;");
            while (statement.Read())
            {
                usages.Add(new I18nIndexedUsage(
                    statement.GetInt64(0),
                    statement.GetInt64(1) == 0
                        ? I18nIndexedUsageKind.Code
                        : I18nIndexedUsageKind.Asset,
                    statement.GetString(2),
                    statement.GetString(3),
                    checked((int)statement.GetInt64(4)),
                    statement.GetString(5),
                    statement.GetString(6),
                    statement.GetString(7)));
            }

            return new I18nUsageIndexSnapshot(status, usages.AsReadOnly());
        }
    }
}
