#nullable enable

using System;
using System.Diagnostics;
using System.Threading;

namespace GreenBox.I18n.Usage.Analysis
{
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
            if (_captureManagedMemory)
            {
                Observe(GC.GetTotalMemory(false));
            }
        }

        public void Observe(long managedMemoryBytes)
        {
            if (!_captureManagedMemory)
            {
                return;
            }

            long observedPeak = Volatile.Read(ref _peakManagedMemoryBytes);
            while (managedMemoryBytes > observedPeak)
            {
                long previousPeak = Interlocked.CompareExchange(
                    ref _peakManagedMemoryBytes,
                    managedMemoryBytes,
                    observedPeak);
                if (previousPeak == observedPeak)
                {
                    return;
                }

                observedPeak = previousPeak;
            }
        }

        public I18nUsageScanPerformance Complete()
        {
            Sample();
            _stopwatch.Stop();
            return CreatePerformance();
        }

        public I18nUsageScanPerformance Snapshot()
        {
            Sample();
            return CreatePerformance();
        }

        private I18nUsageScanPerformance CreatePerformance()
        {
            return new I18nUsageScanPerformance(
                _stopwatch.ElapsedMilliseconds,
                _initialManagedMemoryBytes,
                Volatile.Read(ref _peakManagedMemoryBytes));
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
