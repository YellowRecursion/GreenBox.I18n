#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;

namespace GreenBox.I18n.Usage.Index
{
    /// <summary>
    /// Persists immutable scanner results as an incrementally replaceable local SQLite index.
    /// </summary>
    internal sealed class I18nUsageIndexStore
    {
        private const string AssetKind = I18nUsageIndexSourceWriter.AssetKind;
        private const string AssemblyKind = I18nUsageIndexSourceWriter.AssemblyKind;
        private const int BusyTimeoutMilliseconds = 5000;

        public I18nUsageIndexStore(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("A database path is required.", nameof(databasePath));
            }

            DatabasePath = Path.GetFullPath(databasePath);
        }

        public string DatabasePath { get; }

        public void EnsureCreated()
        {
            using I18nSqliteConnection connection = OpenConnection();
        }

        public I18nUsageIndexSnapshot ReadCurrentUsages()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginReadTransaction();
            I18nUsageIndexSnapshot snapshot = I18nUsageIndexReader.ReadCurrent(connection);
            transaction.Commit();
            return snapshot;
        }

        public void RecoverInterruptedUpdate()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            connection.Execute(@"
UPDATE sources
SET status = 'current', error = NULL
WHERE status = 'pending'
  AND (
      EXISTS(SELECT 1 FROM code_usages WHERE code_usages.source_id = sources.source_id)
      OR EXISTS(SELECT 1 FROM asset_usages WHERE asset_usages.source_id = sources.source_id)
  );

DELETE FROM sources
WHERE status = 'pending';");
            if (GetIndexStatus(connection, transaction) == "updating")
            {
                string status = HasCompletedBaseline(connection, transaction) ? "ready" : "empty";
                UpdateIndexState(connection, transaction, status, null, false);
            }

            transaction.Commit();
        }

        public bool RequiresFullUpdate()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            string status = GetIndexStatus(connection, transaction);
            transaction.Commit();
            return status == "empty" || status == "error";
        }

        public void SetDisabled()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            UpdateIndexState(connection, transaction, "disabled", null, false);
            transaction.Commit();
        }

        public void SetEnabled()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            using I18nSqliteStatement command = connection.Prepare(@"
UPDATE index_state
SET status = 'empty', last_error = NULL
WHERE id = 1 AND status = 'disabled';");
            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public void BeginFullUpdate()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            UpdateIndexState(connection, transaction, "updating", null, false);
            transaction.Commit();
        }

        public void CancelFullUpdate()
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            string status = HasCompletedBaseline(connection, transaction) ? "ready" : "empty";
            UpdateIndexState(connection, transaction, status, null, false);
            transaction.Commit();
        }

        public void BeginAssetUpdate(IEnumerable<string> sourceKeys)
        {
            BeginPartialUpdate(AssetKind, sourceKeys);
        }

        public void BeginAssemblyUpdate(IEnumerable<string> sourceKeys)
        {
            BeginPartialUpdate(AssemblyKind, sourceKeys);
        }

        public void ApplyFull(
            I18nIlUsageScanResult ilResult,
            I18nAssetUsageScanResult assetResult)
        {
            if (!ilResult.IsStable || !assetResult.IsStable)
            {
                throw new InvalidOperationException("A full index update requires stable scan results.");
            }

            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            I18nUsageIndexSourceWriter.ReconcileSources(
                connection,
                transaction,
                AssetKind,
                assetResult.Sources.Select(source => source.SourceKey));
            I18nUsageIndexSourceWriter.ReconcileSources(
                connection,
                transaction,
                AssemblyKind,
                ilResult.Sources.Select(source => source.SourceKey));
            I18nUsageIndexSourceWriter.ApplyAssets(connection, transaction, assetResult.Sources);
            I18nUsageIndexSourceWriter.ApplyAssemblies(connection, transaction, ilResult.Sources);
            UpdateIndexState(connection, transaction, "ready", null, true);
            transaction.Commit();
        }

        public void ApplyAssets(I18nAssetUsageScanResult result)
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            I18nUsageIndexSourceWriter.ApplyAssets(connection, transaction, result.Sources);
            CompletePartialUpdate(
                connection,
                transaction,
                result.Sources.Any(source => source.Status == I18nUsageSourceScanStatus.Success));
            transaction.Commit();
        }

        public void ApplyAssemblies(I18nIlUsageScanResult result)
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            I18nUsageIndexSourceWriter.ApplyAssemblies(connection, transaction, result.Sources);
            CompletePartialUpdate(
                connection,
                transaction,
                result.Sources.Any(source => source.Status == I18nUsageSourceScanStatus.Success));
            transaction.Commit();
        }

        public void RemoveAssets(IEnumerable<string> sourceKeys)
        {
            RemoveSources(AssetKind, sourceKeys);
        }

        public void RemoveAssemblies(IEnumerable<string> sourceKeys)
        {
            RemoveSources(AssemblyKind, sourceKeys);
        }

        public void FailFullUpdate(Exception exception)
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            string status = HasCompletedBaseline(connection, transaction) ? "ready" : "error";
            UpdateIndexState(connection, transaction, status, FormatError(exception), false);
            transaction.Commit();
        }

        public void FailAssetUpdate(IEnumerable<string> sourceKeys, Exception exception)
        {
            FailPartialUpdate(AssetKind, sourceKeys, exception);
        }

        public void FailAssemblyUpdate(IEnumerable<string> sourceKeys, Exception exception)
        {
            FailPartialUpdate(AssemblyKind, sourceKeys, exception);
        }

        private void BeginPartialUpdate(string kind, IEnumerable<string> sourceKeys)
        {
            string[] normalizedSourceKeys = I18nUsageIndexSourceWriter
                .NormalizeSourceKeys(sourceKeys)
                .ToArray();
            if (normalizedSourceKeys.Length == 0)
            {
                return;
            }

            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            foreach (string sourceKey in normalizedSourceKeys)
            {
                I18nUsageIndexSourceWriter.UpsertSource(
                    connection,
                    transaction,
                    kind,
                    sourceKey,
                    null,
                    "pending",
                    null);
            }

            string indexStatus = GetIndexStatus(connection, transaction);
            if (indexStatus == "ready" || indexStatus == "updating")
            {
                UpdateIndexState(connection, transaction, "updating", null, false);
            }

            transaction.Commit();
        }

        private void FailPartialUpdate(
            string kind,
            IEnumerable<string> sourceKeys,
            Exception exception)
        {
            string error = FormatError(exception);
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            foreach (string sourceKey in I18nUsageIndexSourceWriter.NormalizeSourceKeys(sourceKeys))
            {
                I18nUsageIndexSourceWriter.UpsertSource(
                    connection,
                    transaction,
                    kind,
                    sourceKey,
                    null,
                    "failed",
                    error);
            }

            CompletePartialUpdate(connection, transaction, false);
            transaction.Commit();
        }

        private void RemoveSources(string kind, IEnumerable<string> sourceKeys)
        {
            using I18nSqliteConnection connection = OpenConnection();
            using I18nSqliteTransaction transaction = connection.BeginTransaction();
            using I18nSqliteStatement command = connection.Prepare(@"
DELETE FROM sources
WHERE kind = @kind AND source_key = @source_key;");
            foreach (string sourceKey in I18nUsageIndexSourceWriter.NormalizeSourceKeys(sourceKeys))
            {
                command.Bind("@kind", kind).Bind("@source_key", sourceKey);
                command.ExecuteNonQuery();
            }

            CompletePartialUpdate(connection, transaction, true);
            transaction.Commit();
        }

        private static void CompletePartialUpdate(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            bool updateTimestamp)
        {
            string indexStatus = GetIndexStatus(connection, transaction);
            if (indexStatus == "empty" || indexStatus == "disabled" || indexStatus == "error")
            {
                return;
            }

            using I18nSqliteStatement pendingCommand = connection.Prepare(@"
SELECT EXISTS(SELECT 1 FROM sources WHERE status = 'pending');");
            bool hasPendingSources = pendingCommand.ExecuteScalarInt64() != 0;
            UpdateIndexState(
                connection,
                transaction,
                hasPendingSources ? "updating" : "ready",
                null,
                updateTimestamp);
        }

        private static string GetIndexStatus(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction)
        {
            using I18nSqliteStatement command = connection.Prepare(@"
SELECT status FROM index_state WHERE id = 1;");
            return command.ExecuteScalarString();
        }

        private static bool HasCompletedBaseline(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction)
        {
            using I18nSqliteStatement command = connection.Prepare(@"
SELECT updated_at_utc IS NOT NULL
FROM index_state
WHERE id = 1;");
            return command.ExecuteScalarInt64() != 0;
        }

        private static void UpdateIndexState(
            I18nSqliteConnection connection,
            I18nSqliteTransaction transaction,
            string status,
            string? error,
            bool updateTimestamp)
        {
            using I18nSqliteStatement command = connection.Prepare(@"
UPDATE index_state
SET status = @status,
    updated_at_utc = CASE WHEN @update_timestamp = 1 THEN @updated_at_utc ELSE updated_at_utc END,
    last_error = @last_error
WHERE id = 1;");
            command
                .Bind("@status", status)
                .Bind("@update_timestamp", updateTimestamp ? 1 : 0)
                .Bind("@updated_at_utc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                .Bind("@last_error", error);
            command.ExecuteNonQuery();
        }

        private I18nSqliteConnection OpenConnection()
        {
            string? directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connection = new I18nSqliteConnection(DatabasePath);
            try
            {
                connection.SetBusyTimeout(BusyTimeoutMilliseconds);
                connection.Execute("PRAGMA foreign_keys = ON;");
                connection.Execute("PRAGMA journal_mode = WAL;");
                connection.Execute("PRAGMA synchronous = NORMAL;");
                I18nUsageIndexSchema.EnsureCreated(connection);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static string FormatError(Exception exception)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }
}
