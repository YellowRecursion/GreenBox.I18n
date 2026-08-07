#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Runs all entry usage scanners and reports their combined result.
    /// </summary>
    internal static class I18nUsageScanner
    {
        private const string MenuPath = "Tools/GreenBox I18n/Scan Entry Usage";
        private const int MaximumReportedLocationCount = 100;
        private const long SlowScanThresholdMilliseconds = 3000;

        internal static event Action<I18nIlUsageScanResult, I18nAssetUsageScanResult>? FullScanCompleted;

        internal static event Action<IReadOnlyList<string>, I18nAssetUsageScanResult>? AssetsScanned;

        internal static event Action<IReadOnlyList<string>, I18nIlUsageScanResult>? AssembliesScanned;

        internal static event Action<IReadOnlyList<string>>? AssetsRemoved;

        internal static event Action<IReadOnlyList<string>>? AssembliesRemoved;

        [MenuItem(MenuPath, false, 100)]
        private static void ScanAndLog()
        {
            if (I18nPreferences.instance.UsageIndexing == I18nUsageIndexingMode.Disabled)
            {
                I18nLog.Warning(
                    "Usage indexing is disabled. Enable it in Preferences > GreenBox > i18n.");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                I18nLog.Warning("Wait for script compilation to finish before scanning entry usage.");
                return;
            }

            long initialRevision = I18nUsageSourceRevisionTracker.CurrentRevision;
            var totalProfiler = new I18nUsageScanProfiler();
            I18nIlUsageScanResult ilResult = I18nIlUsageScanner.Scan();
            totalProfiler.Observe(ilResult.Performance.PeakManagedMemoryBytes);

            I18nAssetUsageScanResult assetResult = I18nAssetUsageScanner.Scan();
            totalProfiler.Observe(assetResult.Performance.PeakManagedMemoryBytes);
            I18nUsageScanPerformance totalPerformance = totalProfiler.Complete();
            bool isStable = ilResult.IsStable &&
                            assetResult.IsStable &&
                            initialRevision == I18nUsageSourceRevisionTracker.CurrentRevision;
            if (isStable)
            {
                FullScanCompleted?.Invoke(ilResult, assetResult);
            }

            bool isSlow = totalPerformance.ElapsedMilliseconds > SlowScanThresholdMilliseconds;
            bool hasWarnings = ilResult.Warnings.Count > 0 || assetResult.Warnings.Count > 0;
            bool includePerformance = isSlow || I18nLog.IsPerformanceEnabled;
            string report = includePerformance
                ? FormatDetailedReport(
                    ilResult,
                    assetResult,
                    totalPerformance,
                    I18nLog.IsVerboseEnabled)
                : FormatSummary(ilResult, assetResult, totalPerformance);

            if (isSlow)
            {
                report = FormatSlowScanWarning("Full usage scan", totalPerformance.ElapsedMilliseconds) +
                         Environment.NewLine + Environment.NewLine +
                         report;
            }

            if (!isStable)
            {
                report = "Usage sources changed while the full scan was running. " +
                         "The result was not published; run the scan again." +
                         Environment.NewLine + Environment.NewLine +
                         report;
            }

            if (isSlow || hasWarnings || !isStable)
            {
                I18nLog.Warning(report);
            }
            else
            {
                I18nLog.Info(report);
            }
        }

        private static string FormatSummary(
            I18nIlUsageScanResult ilResult,
            I18nAssetUsageScanResult assetResult,
            I18nUsageScanPerformance totalPerformance)
        {
            var lines = new List<string>
            {
                $"Usage scan completed in {totalPerformance.ElapsedMilliseconds} ms. " +
                $"Found {ilResult.Usages.Count} IL and {assetResult.Usages.Count} serialized usage(s).",
            };

            if (ilResult.Warnings.Count > 0 || assetResult.Warnings.Count > 0)
            {
                lines.Add("Warnings:");
                foreach (string warning in ilResult.Warnings)
                {
                    lines.Add($"  {warning}");
                }

                foreach (string warning in assetResult.Warnings)
                {
                    lines.Add($"  {warning}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatDetailedReport(
            I18nIlUsageScanResult ilResult,
            I18nAssetUsageScanResult assetResult,
            I18nUsageScanPerformance totalPerformance,
            bool includeLocations)
        {
            string ilReport = I18nIlUsageScanner.FormatReport(
                ilResult,
                MaximumReportedLocationCount,
                out int reportedLocationCount,
                includeLocations);
            string assetReport = I18nAssetUsageReportFormatter.Format(
                assetResult,
                MaximumReportedLocationCount - reportedLocationCount,
                out _,
                includeLocations);
            return FormatPerformance(
                       ilResult.Performance,
                       assetResult.Performance,
                       totalPerformance) +
                   Environment.NewLine + Environment.NewLine +
                   ilReport +
                   Environment.NewLine + Environment.NewLine +
                   assetReport;
        }

        /// <summary>
        /// Scans a changed set of serialized assets and warns only when the operation is slow.
        /// </summary>
        internal static I18nAssetUsageScanResult ScanAssets(IReadOnlyList<string> assetPaths)
        {
            string[] sourceKeys = assetPaths
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            I18nUsageRevisionBatch revisionBatch = I18nUsageSourceRevisionTracker.Capture(sourceKeys);
            I18nAssetUsageScanResult result = I18nAssetUsageScanner.Scan(assetPaths);
            result = result.MarkChangedSources(
                I18nUsageSourceRevisionTracker.FindChanged(revisionBatch));
            AssetsScanned?.Invoke(assetPaths, result);
            if (!result.IsStable)
            {
                I18nUsageAutoScanner.RequeueAssets(result.ChangedSourcePaths);
                I18nLog.Warning(FormatChangedSourcesWarning(
                    "Asset usage scan",
                    result.ChangedSourcePaths,
                    I18nUsageAutoScanner.IsEnabled));
            }

            if (result.ElapsedMilliseconds > SlowScanThresholdMilliseconds)
            {
                (string stageName, double stageMilliseconds) = FindSlowestAssetStage(result.Diagnostics);
                string warning =
                    FormatSlowScanWarning("Partial asset usage scan", result.ElapsedMilliseconds) +
                    $" Scanned {result.ScannedAssetCount} asset file(s); " +
                    $"slowest stage: {stageName} ({stageMilliseconds:0.0} ms).";
                if (I18nLog.IsPerformanceEnabled)
                {
                    warning += Environment.NewLine + Environment.NewLine +
                               I18nAssetUsageReportFormatter.Format(
                                   result,
                                   MaximumReportedLocationCount,
                                   out _,
                                   I18nLog.IsVerboseEnabled);
                }

                I18nLog.Warning(warning);
                return result;
            }

            if (I18nLog.IsPerformanceEnabled)
            {
                I18nLog.Performance(I18nAssetUsageReportFormatter.Format(
                    result,
                    MaximumReportedLocationCount,
                    out _,
                    I18nLog.IsVerboseEnabled));
            }

            return result;
        }

        /// <summary>
        /// Scans a changed set of compiled assemblies and warns only when the operation is slow.
        /// </summary>
        internal static I18nIlUsageScanResult ScanAssemblies(IReadOnlyList<string> assemblyPaths)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)!.FullName;
            string[] sourceKeys = assemblyPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => I18nUsagePath.Resolve(path, projectRoot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            I18nUsageRevisionBatch revisionBatch = I18nUsageSourceRevisionTracker.Capture(sourceKeys);
            I18nIlUsageScanResult result = I18nIlUsageScanner.Scan(assemblyPaths);
            result = result.MarkChangedSources(
                I18nUsageSourceRevisionTracker.FindChanged(revisionBatch));
            AssembliesScanned?.Invoke(assemblyPaths, result);
            if (!result.IsStable)
            {
                string[] removedAssemblies = result.ChangedSourcePaths
                    .Where(path => !File.Exists(path))
                    .ToArray();
                string[] existingAssemblies = result.ChangedSourcePaths
                    .Where(File.Exists)
                    .ToArray();
                if (removedAssemblies.Length > 0)
                {
                    AssembliesRemoved?.Invoke(removedAssemblies);
                }

                I18nUsageAutoScanner.RequeueAssemblies(existingAssemblies);
                I18nLog.Warning(FormatChangedSourcesWarning(
                    "IL usage scan",
                    result.ChangedSourcePaths,
                    I18nUsageAutoScanner.IsEnabled));
            }

            if (result.ElapsedMilliseconds > SlowScanThresholdMilliseconds)
            {
                string warning =
                    FormatSlowScanWarning("Partial IL usage scan", result.ElapsedMilliseconds) +
                    $" Scanned {result.ScannedAssemblyCount} player assembly(s); " +
                    $"{result.CandidateAssemblyCount} required Cecil analysis.";
                if (I18nLog.IsPerformanceEnabled)
                {
                    warning += Environment.NewLine + Environment.NewLine +
                               I18nIlUsageScanner.FormatReport(
                                   result,
                                   MaximumReportedLocationCount,
                                   out _,
                                   I18nLog.IsVerboseEnabled);
                }

                I18nLog.Warning(warning);
                return result;
            }

            if (I18nLog.IsPerformanceEnabled)
            {
                I18nLog.Performance(I18nIlUsageScanner.FormatReport(
                    result,
                    MaximumReportedLocationCount,
                    out _,
                    I18nLog.IsVerboseEnabled));
            }

            return result;
        }

        internal static void RemoveAssets(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths.Count > 0)
            {
                AssetsRemoved?.Invoke(assetPaths);
            }
        }

        private static string FormatSlowScanWarning(string operation, long elapsedMilliseconds)
        {
            return $"{operation} took {elapsedMilliseconds} ms, exceeding the " +
                   $"{SlowScanThresholdMilliseconds} ms warning threshold.";
        }

        private static string FormatChangedSourcesWarning(
            string operation,
            IReadOnlyList<string> changedSources,
            bool wasRequeued)
        {
            string action = wasRequeued
                ? "The stale result was discarded and a replacement update was scheduled."
                : "The stale result was discarded; run the scan again.";
            string sources = string.Join(", ", changedSources.Take(3));
            if (changedSources.Count > 3)
            {
                sources += $", and {changedSources.Count - 3} more";
            }

            return $"{operation} observed {changedSources.Count} source change(s) while running: " +
                   $"{sources}. {action}";
        }

        private static (string Name, double Milliseconds) FindSlowestAssetStage(
            I18nAssetUsageScanDiagnostics diagnostics)
        {
            string name = "discover paths";
            double milliseconds = diagnostics.PathDiscoveryMilliseconds;
            Observe("read + marker prefilter", diagnostics.MarkerPrefilterMilliseconds);
            Observe("AssetDatabase metadata", diagnostics.AssetMetadataMilliseconds);
            Observe("parse matched YAML", diagnostics.YamlParseMilliseconds);
            Observe("resolve contexts", diagnostics.ContextResolutionMilliseconds);
            Observe("build results", diagnostics.ResultBuildMilliseconds);
            return (name, milliseconds);

            void Observe(string candidateName, double candidateMilliseconds)
            {
                if (candidateMilliseconds <= milliseconds)
                {
                    return;
                }

                name = candidateName;
                milliseconds = candidateMilliseconds;
            }
        }

        private static string FormatPerformance(
            I18nUsageScanPerformance ilPerformance,
            I18nUsageScanPerformance assetPerformance,
            I18nUsageScanPerformance totalPerformance)
        {
            return string.Join(
                Environment.NewLine,
                "Usage scan performance (observed managed-heap peak):",
                FormatPerformanceLine("IL code", ilPerformance),
                FormatPerformanceLine("Unity assets", assetPerformance),
                FormatPerformanceLine("Total", totalPerformance));
        }

        private static string FormatPerformanceLine(
            string name,
            I18nUsageScanPerformance performance)
        {
            return $"  {name}: {performance.ElapsedMilliseconds} ms, " +
                   $"peak +{FormatByteCount(performance.PeakMemoryIncreaseBytes)}";
        }

        private static string FormatByteCount(long byteCount)
        {
            if (byteCount < 1024)
            {
                return $"{byteCount} B";
            }

            if (byteCount < 1024 * 1024)
            {
                return $"{byteCount / 1024d:0.0} KiB";
            }

            return $"{byteCount / (1024d * 1024d):0.0} MiB";
        }
    }

}
