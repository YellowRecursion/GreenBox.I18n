#nullable enable

using System;
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

            if (ilResult.Warnings.Count > 0 || assetResult.Warnings.Count > 0)
            {
                I18nLog.Warning(report);
            }
            else
            {
                I18nLog.Info(report);
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
        private readonly Stopwatch? _stopwatch;
        private readonly long _initialManagedMemoryBytes;
        private long _peakManagedMemoryBytes;

        public I18nUsageScanProfiler(bool enabled = true)
        {
            if (!enabled)
            {
                return;
            }

            _stopwatch = Stopwatch.StartNew();
            _initialManagedMemoryBytes = GC.GetTotalMemory(false);
            _peakManagedMemoryBytes = _initialManagedMemoryBytes;
        }

        public void Sample()
        {
            if (_stopwatch == null)
            {
                return;
            }

            Observe(GC.GetTotalMemory(false));
        }

        public void Observe(long managedMemoryBytes)
        {
            if (_stopwatch == null)
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
            if (_stopwatch == null)
            {
                return new I18nUsageScanPerformance(0, 0, 0);
            }

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
