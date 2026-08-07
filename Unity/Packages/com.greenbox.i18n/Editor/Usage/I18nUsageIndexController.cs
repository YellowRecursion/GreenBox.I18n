#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using GreenBox.I18n.Usage.Index;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Connects Unity scan lifecycle events to the local usage index.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nUsageIndexController
    {
        private static readonly I18nUsageIndexStore Store = new(GetDatabasePath());

        static I18nUsageIndexController()
        {
            I18nUsageScanner.FullScanStarted += HandleFullScanStarted;
            I18nUsageScanner.FullScanCompleted += HandleFullScanCompleted;
            I18nUsageScanner.FullScanInvalidated += HandleFullScanInvalidated;
            I18nUsageScanner.FullScanFailed += HandleFullScanFailed;
            I18nUsageScanner.AssetsScanStarted += HandleAssetsScanStarted;
            I18nUsageScanner.AssetsScanned += HandleAssetsScanned;
            I18nUsageScanner.AssetsScanFailed += HandleAssetsScanFailed;
            I18nUsageScanner.AssembliesScanStarted += HandleAssembliesScanStarted;
            I18nUsageScanner.AssembliesScanned += HandleAssembliesScanned;
            I18nUsageScanner.AssembliesScanFailed += HandleAssembliesScanFailed;
            I18nUsageScanner.AssetsRemoved += HandleAssetsRemoved;
            I18nUsageScanner.AssembliesRemoved += HandleAssembliesRemoved;
            I18nPreferences.UsageIndexingChanged += HandleUsageIndexingChanged;

            Execute("initialize the usage index", () =>
            {
                Store.EnsureCreated();
                Store.RecoverInterruptedUpdate();
                SynchronizeMode(I18nPreferences.instance.UsageIndexing);
            });
        }

        internal static string DatabasePath => Store.DatabasePath;

        private static void HandleFullScanStarted()
        {
            Execute("mark a full usage update as started", Store.BeginFullUpdate);
        }

        private static void HandleFullScanCompleted(
            I18nIlUsageScanResult ilResult,
            I18nAssetUsageScanResult assetResult)
        {
            Execute(
                "write the full usage index",
                () => Store.ApplyFull(ilResult, assetResult),
                Store.FailFullUpdate);
        }

        private static void HandleFullScanFailed(Exception exception)
        {
            Execute("record a failed full usage update", () => Store.FailFullUpdate(exception));
        }

        private static void HandleFullScanInvalidated()
        {
            Execute("cancel an invalidated full usage update", Store.CancelFullUpdate);
            if (I18nUsageAutoScanner.IsEnabled)
            {
                I18nUsageAutoScanner.QueueFullScan();
            }
        }

        private static void HandleAssetsScanStarted(IReadOnlyList<string> assetPaths)
        {
            Execute(
                "mark asset usage sources as pending",
                () => Store.BeginAssetUpdate(NormalizeAssetPaths(assetPaths)));
        }

        private static void HandleAssetsScanned(
            IReadOnlyList<string> assetPaths,
            I18nAssetUsageScanResult result)
        {
            Execute("write asset usage results", () => Store.ApplyAssets(result));
        }

        private static void HandleAssetsScanFailed(
            IReadOnlyList<string> assetPaths,
            Exception exception)
        {
            Execute(
                "record failed asset usage sources",
                () => Store.FailAssetUpdate(NormalizeAssetPaths(assetPaths), exception));
        }

        private static void HandleAssembliesScanStarted(IReadOnlyList<string> assemblyPaths)
        {
            Execute(
                "mark assembly usage sources as pending",
                () => Store.BeginAssemblyUpdate(GetAssemblyKeys(assemblyPaths)));
        }

        private static void HandleAssembliesScanned(
            IReadOnlyList<string> assemblyPaths,
            I18nIlUsageScanResult result)
        {
            Execute("write assembly usage results", () => Store.ApplyAssemblies(result));
        }

        private static void HandleAssembliesScanFailed(
            IReadOnlyList<string> assemblyPaths,
            Exception exception)
        {
            Execute(
                "record failed assembly usage sources",
                () => Store.FailAssemblyUpdate(GetAssemblyKeys(assemblyPaths), exception));
        }

        private static void HandleAssetsRemoved(IReadOnlyList<string> assetPaths)
        {
            Execute(
                "remove deleted assets from the usage index",
                () => Store.RemoveAssets(NormalizeAssetPaths(assetPaths)));
        }

        private static void HandleAssembliesRemoved(IReadOnlyList<string> assemblyPaths)
        {
            Execute(
                "remove deleted assemblies from the usage index",
                () => Store.RemoveAssemblies(GetAssemblyKeys(assemblyPaths)));
        }

        private static void HandleUsageIndexingChanged(I18nUsageIndexingMode mode)
        {
            Execute("update the usage index mode", () => SynchronizeMode(mode));
        }

        private static void SynchronizeMode(I18nUsageIndexingMode mode)
        {
            if (mode == I18nUsageIndexingMode.Disabled)
            {
                Store.SetDisabled();
            }
            else
            {
                Store.SetEnabled();
                if (mode == I18nUsageIndexingMode.Automatic && Store.RequiresFullUpdate())
                {
                    I18nUsageAutoScanner.QueueFullScan();
                }
            }
        }

        private static IEnumerable<string> NormalizeAssetPaths(IEnumerable<string> assetPaths)
        {
            return assetPaths.Select(path => path.Replace('\\', '/'));
        }

        private static IEnumerable<string> GetAssemblyKeys(IEnumerable<string> assemblyPaths)
        {
            return assemblyPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFileNameWithoutExtension);
        }

        private static string GetDatabasePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, "Library", "GreenBox.I18n", "usage-index.db");
        }

        private static void Execute(
            string operation,
            Action action,
            Action<Exception>? failureAction = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                if (failureAction != null)
                {
                    try
                    {
                        failureAction(exception);
                    }
                    catch
                    {
                        // The original storage failure is the actionable error.
                    }
                }

                I18nLog.Error(
                    $"Failed to {operation}: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
