#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GreenBox.I18n.Usage.Analysis
{
    /// <summary>
    /// Finds localization keys in Unity text-serialized scenes, prefabs, and assets.
    /// </summary>
    internal static class I18nAssetUsageAnalyzer
    {
        private const int ReadBufferSize = 64 * 1024;
        private const int MaximumReportedSlowFileCount = 5;

        /// <summary>
        /// Analyzes prepared Unity text assets without accessing Unity APIs.
        /// </summary>
        internal static I18nAssetUsageScanResult Analyze(
            IEnumerable<I18nSerializedAssetSource> sources,
            I18nUsageScanProfiler profiler,
            List<string> warnings,
            IReadOnlyList<string> initiallyChangedSourcePaths)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var models = new List<I18nSerializedAssetModel>();
            int scannedAssetCount = 0;
            long scannedByteCount = 0;
            byte[] readBuffer = new byte[ReadBufferSize];
            long pathDiscoveryTicks = 0;
            long markerPrefilterTicks = 0;
            long assetMetadataTicks = 0;
            long yamlParseTicks = 0;
            long contextResolutionTicks = 0;
            var slowestFiles = new SlowestFileCollector(MaximumReportedSlowFileCount);
            var initialSources = new Dictionary<string, I18nSerializedAssetObservation>(
                StringComparer.OrdinalIgnoreCase);

            using IEnumerator<I18nSerializedAssetSource> pathEnumerator = sources.GetEnumerator();
            while (MoveNext(pathEnumerator, ref pathDiscoveryTicks))
            {
                I18nSerializedAssetSource source = pathEnumerator.Current;
                string absolutePath = source.AbsolutePath;
                initialSources[absolutePath] = new I18nSerializedAssetObservation(
                    absolutePath,
                    source.AssetPath,
                    I18nUsageSourceStamp.Capture(absolutePath, absolutePath + ".meta"));
                long fileStart = Stopwatch.GetTimestamp();
                scannedAssetCount++;
                try
                {
                    bool containsEntryIdMarker;
                    long stageStart = Stopwatch.GetTimestamp();
                    try
                    {
                        scannedByteCount += new FileInfo(absolutePath).Length;
                        containsEntryIdMarker = I18nAssetMarkerPrefilter.Contains(absolutePath, readBuffer);
                    }
                    finally
                    {
                        markerPrefilterTicks += Stopwatch.GetTimestamp() - stageStart;
                    }

                    if (!containsEntryIdMarker)
                    {
                        continue;
                    }

                    string assetGuid;
                    stageStart = Stopwatch.GetTimestamp();
                    try
                    {
                        assetGuid = I18nAssetMetadataReader.ReadGuid(absolutePath + ".meta");
                    }
                    finally
                    {
                        assetMetadataTicks += Stopwatch.GetTimestamp() - stageStart;
                    }

                    I18nSerializedAssetModel model;
                    stageStart = Stopwatch.GetTimestamp();
                    try
                    {
                        model = I18nUnityYamlParser.Parse(
                            absolutePath,
                            source.AssetPath,
                            assetGuid,
                            warnings);
                    }
                    finally
                    {
                        yamlParseTicks += Stopwatch.GetTimestamp() - stageStart;
                    }

                    profiler.Sample();
                    stageStart = Stopwatch.GetTimestamp();
                    try
                    {
                        model.PrepareContexts();
                    }
                    finally
                    {
                        contextResolutionTicks += Stopwatch.GetTimestamp() - stageStart;
                    }

                    profiler.Sample();
                    models.Add(model);
                }
                catch (Exception exception)
                {
                    warnings.Add(
                        $"Skipped '{source.AssetPath}': " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
                finally
                {
                    slowestFiles.Observe(
                        source.AssetPath,
                        Stopwatch.GetTimestamp() - fileStart);
                }
            }

            long resultBuildStart = Stopwatch.GetTimestamp();
            IReadOnlyDictionary<string, I18nSerializedAssetModel> modelsByGuid = models
                .Where(model => model.AssetGuid.Length > 0)
                .GroupBy(model => model.AssetGuid, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            I18nAssetUsage[] usages = models
                .SelectMany(model => model.CreateUsages(modelsByGuid))
                .OrderBy(usage => usage.EntryId)
                .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                .ThenBy(usage => usage.Line)
                .ToArray();
            long resultBuildTicks = Stopwatch.GetTimestamp() - resultBuildStart;
            string[] changedSourcePaths = initialSources
                .Where(pair => pair.Value.Stamp != I18nUsageSourceStamp.Capture(
                    pair.Key,
                    pair.Key + ".meta"))
                .Select(pair => pair.Value.AssetPath)
                .Concat(initiallyChangedSourcePaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string changedSourcePath in changedSourcePaths)
            {
                warnings.Add(
                    $"'{changedSourcePath}' changed while the scan was being prepared or run; " +
                    "the scan result will not be published.");
            }

            I18nUsageScanPerformance performance = profiler.Snapshot();
            var diagnostics = new I18nAssetUsageScanDiagnostics(
                ToMilliseconds(pathDiscoveryTicks),
                ToMilliseconds(markerPrefilterTicks),
                ToMilliseconds(assetMetadataTicks),
                ToMilliseconds(yamlParseTicks),
                ToMilliseconds(contextResolutionTicks),
                ToMilliseconds(resultBuildTicks),
                slowestFiles.CreateResult());
            return new I18nAssetUsageScanResult(
                usages,
                warnings,
                scannedAssetCount,
                models.Count,
                scannedByteCount,
                performance,
                diagnostics,
                true,
                changedSourcePaths,
                initialSources.Values.ToArray());
        }

        private static bool MoveNext(
            IEnumerator<I18nSerializedAssetSource> enumerator,
            ref long elapsedTicks)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                return enumerator.MoveNext();
            }
            finally
            {
                elapsedTicks += Stopwatch.GetTimestamp() - start;
            }
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private sealed class SlowestFileCollector
        {
            private readonly int _capacity;
            private readonly List<SlowFileCandidate> _files;

            public SlowestFileCollector(int capacity)
            {
                _capacity = capacity;
                _files = new List<SlowFileCandidate>(capacity);
            }

            public void Observe(string assetPath, long elapsedTicks)
            {
                if (_files.Count < _capacity)
                {
                    _files.Add(new SlowFileCandidate(assetPath, elapsedTicks));
                    return;
                }

                int fastestIndex = 0;
                for (int index = 1; index < _files.Count; index++)
                {
                    if (_files[index].ElapsedTicks < _files[fastestIndex].ElapsedTicks)
                    {
                        fastestIndex = index;
                    }
                }

                if (elapsedTicks > _files[fastestIndex].ElapsedTicks)
                {
                    _files[fastestIndex] = new SlowFileCandidate(assetPath, elapsedTicks);
                }
            }

            public IReadOnlyList<I18nAssetFileScanPerformance> CreateResult()
            {
                return _files
                    .OrderByDescending(file => file.ElapsedTicks)
                    .Select(file => new I18nAssetFileScanPerformance(
                        file.AssetPath,
                        ToMilliseconds(file.ElapsedTicks)))
                    .ToArray();
            }

            private sealed class SlowFileCandidate
            {
                public SlowFileCandidate(string assetPath, long elapsedTicks)
                {
                    AssetPath = assetPath;
                    ElapsedTicks = elapsedTicks;
                }

                public string AssetPath { get; }

                public long ElapsedTicks { get; }
            }
        }

    }

}
