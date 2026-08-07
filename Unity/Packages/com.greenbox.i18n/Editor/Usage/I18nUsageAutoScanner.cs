#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;
using UnityEditor.Compilation;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Coalesces Unity change notifications and runs targeted usage scans while the Editor is idle.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nUsageAutoScanner
    {
        private const string PendingAssetPathsKey = "GreenBox.I18n.Usage.PendingAssets";
        private const string RemovedAssetPathsKey = "GreenBox.I18n.Usage.RemovedAssets";
        private const string PendingAssemblyPathsKey = "GreenBox.I18n.Usage.PendingAssemblies";

        private static readonly HashSet<string> PendingAssetPaths = LoadPaths(
            PendingAssetPathsKey,
            StringComparer.Ordinal);

        private static readonly HashSet<string> RemovedAssetPaths = LoadPaths(
            RemovedAssetPathsKey,
            StringComparer.Ordinal);

        private static readonly HashSet<string> PendingAssemblyPaths = LoadPaths(
            PendingAssemblyPathsKey,
            StringComparer.OrdinalIgnoreCase);

        private static bool _isScheduled;

        static I18nUsageAutoScanner()
        {
            CompilationPipeline.assemblyCompilationFinished += HandleAssemblyCompilationFinished;
            if (PendingAssetPaths.Count > 0 ||
                RemovedAssetPaths.Count > 0 ||
                PendingAssemblyPaths.Count > 0)
            {
                Schedule();
            }
        }

        internal static bool IsEnabled => IsAutomatic;

        internal static void QueueAssetChanges(
            IReadOnlyList<string> changedPaths,
            IReadOnlyList<string> removedPaths)
        {
            if (!IsAutomatic)
            {
                return;
            }

            bool changed = false;
            foreach (string path in removedPaths)
            {
                string normalizedPath = NormalizeAssetPath(path);
                if (!IsScannableAssetPath(normalizedPath))
                {
                    continue;
                }

                changed |= RemovedAssetPaths.Add(normalizedPath);
                changed |= PendingAssetPaths.Remove(normalizedPath);
            }

            foreach (string path in changedPaths)
            {
                string normalizedPath = NormalizeAssetPath(path);
                if (!IsScannableAssetPath(normalizedPath))
                {
                    continue;
                }

                changed |= PendingAssetPaths.Add(normalizedPath);
                changed |= RemovedAssetPaths.Remove(normalizedPath);
            }

            if (!changed)
            {
                return;
            }

            SavePaths(PendingAssetPathsKey, PendingAssetPaths);
            SavePaths(RemovedAssetPathsKey, RemovedAssetPaths);
            Schedule();
        }

        private static bool IsAutomatic =>
            I18nPreferences.instance.UsageIndexing == I18nUsageIndexingMode.Automatic;

        private static void HandleAssemblyCompilationFinished(
            string assemblyPath,
            CompilerMessage[] compilerMessages)
        {
            if (!IsAutomatic || compilerMessages.Any(
                    message => message.type == CompilerMessageType.Error))
            {
                return;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(assemblyPath);
            }
            catch
            {
                return;
            }

            if (PendingAssemblyPaths.Add(normalizedPath))
            {
                SavePaths(PendingAssemblyPathsKey, PendingAssemblyPaths);
                Schedule();
            }
        }

        private static void Schedule()
        {
            if (_isScheduled)
            {
                return;
            }

            _isScheduled = true;
            EditorApplication.update += ProcessWhenEditorIsIdle;
        }

        private static void ProcessWhenEditorIsIdle()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= ProcessWhenEditorIsIdle;
            _isScheduled = false;

            if (!IsAutomatic)
            {
                ClearPendingChanges();
                return;
            }

            string[] removedAssets = RemovedAssetPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] changedAssets = PendingAssetPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] changedAssemblies = PendingAssemblyPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            ClearPendingChanges();

            if (removedAssets.Length > 0)
            {
                TryRun("removed asset update", () => I18nUsageScanner.RemoveAssets(removedAssets));
            }

            if (changedAssemblies.Length > 0)
            {
                TryRun(
                    "assembly scan",
                    () => I18nUsageScanner.ScanAssemblies(changedAssemblies));
            }

            if (changedAssets.Length > 0)
            {
                TryRun("asset scan", () => I18nUsageScanner.ScanAssets(changedAssets));
            }
        }

        private static void TryRun(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                I18nLog.Error(
                    $"Automatic usage indexing {operation} failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static bool IsScannableAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   I18nAssetUsageScanner.IsSupportedAssetPath(path);
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace('\\', '/') ?? string.Empty;
        }

        private static HashSet<string> LoadPaths(string key, StringComparer comparer)
        {
            var paths = new HashSet<string>(comparer);
            string serializedPaths = SessionState.GetString(key, string.Empty);
            foreach (string path in serializedPaths.Split(
                         new[] { '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                paths.Add(path);
            }

            return paths;
        }

        private static void SavePaths(string key, IEnumerable<string> paths)
        {
            SessionState.SetString(
                key,
                string.Join("\n", paths.OrderBy(path => path, StringComparer.Ordinal)));
        }

        private static void ClearPendingChanges()
        {
            PendingAssetPaths.Clear();
            RemovedAssetPaths.Clear();
            PendingAssemblyPaths.Clear();
            SessionState.SetString(PendingAssetPathsKey, string.Empty);
            SessionState.SetString(RemovedAssetPathsKey, string.Empty);
            SessionState.SetString(PendingAssemblyPathsKey, string.Empty);
        }
    }
}
