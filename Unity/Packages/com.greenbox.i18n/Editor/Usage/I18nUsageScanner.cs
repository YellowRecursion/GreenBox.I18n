#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using GreenBox.I18n.Unity.Editor.Diagnostics;
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

        [MenuItem(MenuPath, false, 100)]
        private static void ScanAndLog()
        {
            if (EditorApplication.isCompiling)
            {
                I18nLog.Warning("Wait for script compilation to finish before scanning entry usage.");
                return;
            }

            var totalProfiler = new I18nUsageScanProfiler();
            I18nIlUsageScanResult ilResult = I18nIlUsageScanner.Scan();
            totalProfiler.Observe(ilResult.Performance.PeakManagedMemoryBytes);

            I18nAssetUsageScanResult assetResult = I18nAssetUsageScanner.Scan();
            totalProfiler.Observe(assetResult.Performance.PeakManagedMemoryBytes);
            I18nUsageScanPerformance totalPerformance = totalProfiler.Complete();

            string ilReport = I18nIlUsageScanner.FormatReport(
                ilResult,
                MaximumReportedLocationCount,
                out int reportedLocationCount);
            string assetReport = I18nAssetUsageScanner.FormatReport(
                assetResult,
                MaximumReportedLocationCount - reportedLocationCount,
                out _);
            string report = FormatPerformance(
                                ilResult.Performance,
                                assetResult.Performance,
                                totalPerformance) +
                            Environment.NewLine + Environment.NewLine +
                            ilReport +
                            Environment.NewLine + Environment.NewLine +
                            assetReport;

            bool isSlow = totalPerformance.ElapsedMilliseconds > SlowScanThresholdMilliseconds;
            if (isSlow)
            {
                report = FormatSlowScanWarning("Full usage scan", totalPerformance.ElapsedMilliseconds) +
                         Environment.NewLine + Environment.NewLine +
                         report;
            }

            if (isSlow || ilResult.Warnings.Count > 0 || assetResult.Warnings.Count > 0)
            {
                I18nLog.Warning(report);
            }
            else
            {
                I18nLog.Info(report);
            }
        }

        /// <summary>
        /// Scans a changed set of serialized assets and warns only when the operation is slow.
        /// </summary>
        internal static I18nAssetUsageScanResult ScanAssets(IReadOnlyList<string> assetPaths)
        {
            I18nAssetUsageScanResult result = I18nAssetUsageScanner.Scan(assetPaths);
            if (result.ElapsedMilliseconds <= SlowScanThresholdMilliseconds)
            {
                return result;
            }

            (string stageName, double stageMilliseconds) = FindSlowestAssetStage(result.Diagnostics);
            I18nLog.Warning(
                FormatSlowScanWarning("Partial asset usage scan", result.ElapsedMilliseconds) +
                $" Scanned {result.ScannedAssetCount} asset file(s); " +
                $"slowest stage: {stageName} ({stageMilliseconds:0.0} ms).");
            return result;
        }

        private static string FormatSlowScanWarning(string operation, long elapsedMilliseconds)
        {
            return $"{operation} took {elapsedMilliseconds} ms, exceeding the " +
                   $"{SlowScanThresholdMilliseconds} ms warning threshold.";
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

    /// <summary>
    /// Captures low-overhead elapsed-time and managed-heap observations for one scan node.
    /// </summary>
    internal sealed class I18nUsageScanProfiler
    {
        private readonly Stopwatch _stopwatch;
        private readonly bool _captureManagedMemory;
        private readonly long _initialManagedMemoryBytes;
        private long _peakManagedMemoryBytes;

        public I18nUsageScanProfiler(bool captureManagedMemory = true)
        {
            _stopwatch = Stopwatch.StartNew();
            _captureManagedMemory = captureManagedMemory;
            if (!captureManagedMemory)
            {
                return;
            }

            _initialManagedMemoryBytes = GC.GetTotalMemory(false);
            _peakManagedMemoryBytes = _initialManagedMemoryBytes;
        }

        public void Sample()
        {
            if (!_captureManagedMemory)
            {
                return;
            }

            Observe(GC.GetTotalMemory(false));
        }

        public void Observe(long managedMemoryBytes)
        {
            if (!_captureManagedMemory)
            {
                return;
            }

            if (managedMemoryBytes > _peakManagedMemoryBytes)
            {
                _peakManagedMemoryBytes = managedMemoryBytes;
            }
        }

        public I18nUsageScanPerformance Complete()
        {
            Sample();
            _stopwatch.Stop();
            return new I18nUsageScanPerformance(
                _stopwatch.ElapsedMilliseconds,
                _initialManagedMemoryBytes,
                _peakManagedMemoryBytes);
        }
    }

    /// <summary>
    /// Describes the observed performance of one usage scan node.
    /// </summary>
    internal sealed class I18nUsageScanPerformance
    {
        public I18nUsageScanPerformance(
            long elapsedMilliseconds,
            long initialManagedMemoryBytes,
            long peakManagedMemoryBytes)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            InitialManagedMemoryBytes = initialManagedMemoryBytes;
            PeakManagedMemoryBytes = peakManagedMemoryBytes;
        }

        public long ElapsedMilliseconds { get; }

        public long InitialManagedMemoryBytes { get; }

        public long PeakManagedMemoryBytes { get; }

        public long PeakMemoryIncreaseBytes => Math.Max(
            0,
            PeakManagedMemoryBytes - InitialManagedMemoryBytes);
    }
}
