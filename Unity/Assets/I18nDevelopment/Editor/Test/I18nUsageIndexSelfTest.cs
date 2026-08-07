#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using GreenBox.I18n.Usage.Analysis;
using GreenBox.I18n.Usage.Index;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Development.Editor
{
    /// <summary>
    /// Verifies full and incremental usage-index transactions against a temporary database.
    /// </summary>
    internal static class I18nUsageIndexSelfTest
    {
        private const string MenuPath = "Tools/GreenBox I18n/Test Usage Index";
        private const long EntryId = 3857353019861119080;

        [MenuItem(MenuPath, false, 102)]
        private static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string testDirectory = Path.Combine(
                projectRoot,
                "Library",
                "GreenBox.I18n",
                "Tests",
                Guid.NewGuid().ToString("N"));
            string databasePath = Path.Combine(testDirectory, "usage-index.db");
            var errors = new List<string>();

            try
            {
                RunScenarios(databasePath, errors);
            }
            catch (Exception exception)
            {
                errors.Add($"unexpected {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDirectory))
                    {
                        Directory.Delete(testDirectory, true);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"could not remove the temporary database: {exception.Message}");
                }
            }

            if (errors.Count == 0)
            {
                I18nLog.Info("PASS - usage index full and incremental scenarios passed.");
                return;
            }

            I18nLog.Error(
                $"FAIL - usage index self-test found {errors.Count} problem(s):" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors.ConvertAll(error => $"  - {error}")));
        }

        private static void RunScenarios(string databasePath, ICollection<string> errors)
        {
            var store = new I18nUsageIndexStore(databasePath);
            store.EnsureCreated();
            AssertScalar(databasePath, "SELECT status FROM index_state WHERE id = 1;", "empty", errors);

            store.BeginFullUpdate();
            store.ApplyFull(CreateIlResult(), CreateAssetResult(I18nUsageSourceScanStatus.Success, true));
            AssertScalar(databasePath, "SELECT status FROM index_state WHERE id = 1;", "ready", errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM sources;", 2L, errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM code_usages;", 1L, errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM asset_usages;", 1L, errors);

            long successfulUpdate = ReadInt64(
                databasePath,
                "SELECT updated_at_utc FROM index_state WHERE id = 1;");
            store.BeginAssetUpdate(new[] { "Assets/Test.prefab" });
            store.FailAssetUpdate(new[] { "Assets/Test.prefab" }, new IOException("test failure"));
            AssertScalar(databasePath, "SELECT status FROM index_state WHERE id = 1;", "ready", errors);
            AssertScalar(
                databasePath,
                "SELECT status FROM sources WHERE kind = 'asset';",
                "failed",
                errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM asset_usages;", 1L, errors);
            AssertScalar(
                databasePath,
                "SELECT updated_at_utc FROM index_state WHERE id = 1;",
                successfulUpdate,
                errors);

            store.BeginAssetUpdate(new[] { "Assets/Test.prefab" });
            store.ApplyAssets(CreateAssetResult(I18nUsageSourceScanStatus.Changed, false));
            AssertScalar(databasePath, "SELECT status FROM index_state WHERE id = 1;", "updating", errors);
            AssertScalar(
                databasePath,
                "SELECT status FROM sources WHERE kind = 'asset';",
                "pending",
                errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM asset_usages;", 1L, errors);

            store.ApplyAssets(CreateAssetResult(I18nUsageSourceScanStatus.Success, false));
            AssertScalar(databasePath, "SELECT status FROM index_state WHERE id = 1;", "ready", errors);
            AssertScalar(
                databasePath,
                "SELECT status FROM sources WHERE kind = 'asset';",
                "current",
                errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM asset_usages;", 0L, errors);

            store.RemoveAssemblies(new[] { "Assembly-CSharp" });
            AssertScalar(
                databasePath,
                "SELECT COUNT(*) FROM sources WHERE kind = 'assembly';",
                0L,
                errors);
            AssertScalar(databasePath, "SELECT COUNT(*) FROM code_usages;", 0L, errors);
        }

        private static I18nIlUsageScanResult CreateIlResult()
        {
            var source = new I18nIlUsageSourceScanResult(
                "Assembly-CSharp",
                "Library/ScriptAssemblies/Assembly-CSharp.dll",
                I18nUsageSourceScanStatus.Success,
                new[] { new I18nIlUsage(EntryId, "Assets/Test.cs", 12) },
                Array.Empty<string>(),
                null,
                true,
                null);
            return new I18nIlUsageScanResult(
                new[] { source },
                Array.Empty<string>(),
                EmptyPerformance());
        }

        private static I18nAssetUsageScanResult CreateAssetResult(
            I18nUsageSourceScanStatus status,
            bool includeUsage)
        {
            IReadOnlyList<I18nAssetUsage> usages = includeUsage
                ? new[]
                {
                    new I18nAssetUsage(
                        EntryId,
                        "0123456789abcdef0123456789abcdef",
                        "Assets/Test.prefab",
                        11400000,
                        100000,
                        "Canvas / Label",
                        "I18nText",
                        string.Empty,
                        "_key._greenBoxI18nEntryId",
                        42,
                        false,
                        string.Empty,
                        string.Empty,
                        0),
                }
                : Array.Empty<I18nAssetUsage>();
            var source = new I18nAssetUsageSourceScanResult(
                "Assets/Test.prefab",
                "Assets/Test.prefab",
                "0123456789abcdef0123456789abcdef",
                status,
                usages,
                Array.Empty<string>(),
                null,
                includeUsage,
                null);
            return new I18nAssetUsageScanResult(
                new[] { source },
                Array.Empty<string>(),
                1,
                128,
                EmptyPerformance(),
                I18nAssetUsageScanDiagnostics.Empty,
                true);
        }

        private static I18nUsageScanPerformance EmptyPerformance()
        {
            return new I18nUsageScanPerformance(0, 0, 0);
        }

        private static long ReadInt64(string databasePath, string sql)
        {
            using var connection = new I18nSqliteConnection(databasePath);
            return connection.ExecuteScalarInt64(sql);
        }

        private static string ReadString(string databasePath, string sql)
        {
            using var connection = new I18nSqliteConnection(databasePath);
            return connection.ExecuteScalarString(sql);
        }

        private static void AssertScalar(
            string databasePath,
            string sql,
            long expected,
            ICollection<string> errors)
        {
            long actual = ReadInt64(databasePath, sql);
            if (actual != expected)
            {
                errors.Add($"expected {expected}, got {actual} for: {sql}");
            }
        }

        private static void AssertScalar(
            string databasePath,
            string sql,
            string expected,
            ICollection<string> errors)
        {
            string actual = ReadString(databasePath, sql);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                errors.Add($"expected '{expected}', got '{actual}' for: {sql}");
            }
        }
    }
}
