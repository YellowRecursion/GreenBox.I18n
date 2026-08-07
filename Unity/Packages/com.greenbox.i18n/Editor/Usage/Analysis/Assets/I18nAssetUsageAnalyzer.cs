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

            var sourceStates = new List<AssetAnalysisState>();
            int scannedAssetCount = 0;
            long scannedByteCount = 0;
            byte[] readBuffer = new byte[ReadBufferSize];
            long pathDiscoveryTicks = 0;
            long markerPrefilterTicks = 0;
            long assetMetadataTicks = 0;
            long yamlParseTicks = 0;
            long contextResolutionTicks = 0;
            var slowestFiles = new SlowestFileCollector(MaximumReportedSlowFileCount);
            using IEnumerator<I18nSerializedAssetSource> pathEnumerator = sources.GetEnumerator();
            while (MoveNext(pathEnumerator, ref pathDiscoveryTicks))
            {
                I18nSerializedAssetSource source = pathEnumerator.Current;
                string absolutePath = source.AbsolutePath;
                var observation = new I18nSerializedAssetObservation(
                    absolutePath,
                    source.AssetPath,
                    I18nUsageSourceStamp.Capture(absolutePath, absolutePath + ".meta"));
                var sourceState = new AssetAnalysisState(source, observation);
                sourceStates.Add(sourceState);
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

                    sourceState.ContainsI18nData = true;

                    string assetGuid;
                    stageStart = Stopwatch.GetTimestamp();
                    try
                    {
                        assetGuid = I18nAssetMetadataReader.ReadGuid(absolutePath + ".meta");
                        sourceState.AssetGuid = assetGuid;
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
                            sourceState.Warnings);
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
                    sourceState.Model = model;
                }
                catch (Exception exception)
                {
                    sourceState.Error = $"{exception.GetType().Name}: {exception.Message}";
                }
                finally
                {
                    slowestFiles.Observe(
                        source.AssetPath,
                        Stopwatch.GetTimestamp() - fileStart);
                }
            }

            var changedSourcePaths = new HashSet<string>(
                initiallyChangedSourcePaths,
                StringComparer.OrdinalIgnoreCase);
            ObserveChangedSources(sourceStates, changedSourcePaths);

            I18nSerializedAssetModel[] stableModels = sourceStates
                .Where(state => state.Error == null &&
                                !changedSourcePaths.Contains(state.Source.AssetPath) &&
                                state.Model != null)
                .Select(state => state.Model!)
                .ToArray();
            long resultBuildStart = Stopwatch.GetTimestamp();
            IReadOnlyDictionary<string, I18nSerializedAssetModel> modelsByGuid = stableModels
                .Where(model => model.AssetGuid.Length > 0)
                .GroupBy(model => model.AssetGuid, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            I18nAssetUsage[] usages = stableModels
                .SelectMany(model => model.CreateUsages(modelsByGuid))
                .OrderBy(usage => usage.EntryId)
                .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                .ThenBy(usage => usage.Line)
                .ToArray();
            long resultBuildTicks = Stopwatch.GetTimestamp() - resultBuildStart;
            ObserveChangedSources(sourceStates, changedSourcePaths);

            var changedAssetGuids = new HashSet<string>(sourceStates
                .Where(state => changedSourcePaths.Contains(state.Source.AssetPath) &&
                                state.AssetGuid.Length > 0)
                .Select(state => state.AssetGuid), StringComparer.Ordinal);
            if (changedAssetGuids.Count > 0)
            {
                foreach (string dependentSourcePath in usages
                             .Where(usage => usage.IsPrefabOverride &&
                                             changedAssetGuids.Contains(usage.TargetAssetGuid))
                             .Select(usage => usage.AssetPath))
                {
                    changedSourcePaths.Add(dependentSourcePath);
                }
            }

            ILookup<string, I18nAssetUsage> usagesBySource = usages.ToLookup(
                usage => usage.AssetPath,
                StringComparer.OrdinalIgnoreCase);
            var sourceResults = new List<I18nAssetUsageSourceScanResult>(sourceStates.Count);
            foreach (AssetAnalysisState sourceState in sourceStates)
            {
                I18nUsageSourceScanStatus status = changedSourcePaths.Contains(sourceState.Source.AssetPath)
                    ? I18nUsageSourceScanStatus.Changed
                    : sourceState.Error != null
                        ? I18nUsageSourceScanStatus.Failed
                        : I18nUsageSourceScanStatus.Success;
                if (status == I18nUsageSourceScanStatus.Changed)
                {
                    sourceState.Warnings.Add(
                        $"'{sourceState.Source.AssetPath}' changed while the scan was being prepared or run.");
                }

                sourceResults.Add(new I18nAssetUsageSourceScanResult(
                    sourceState.Source.AssetPath,
                    sourceState.Source.AbsolutePath,
                    sourceState.AssetGuid,
                    status,
                    status == I18nUsageSourceScanStatus.Success
                        ? usagesBySource[sourceState.Source.AssetPath].ToArray()
                        : Array.Empty<I18nAssetUsage>(),
                    sourceState.Warnings,
                    sourceState.Error,
                    sourceState.ContainsI18nData,
                    sourceState.Observation));
            }

            foreach (string changedSourcePath in changedSourcePaths
                         .Where(path => sourceStates.All(state => !string.Equals(
                             state.Source.AssetPath,
                             path,
                             StringComparison.OrdinalIgnoreCase))))
            {
                sourceResults.Add(new I18nAssetUsageSourceScanResult(
                    changedSourcePath,
                    string.Empty,
                    string.Empty,
                    I18nUsageSourceScanStatus.Changed,
                    Array.Empty<I18nAssetUsage>(),
                    new[] { $"'{changedSourcePath}' changed before it could be scanned." },
                    null,
                    false,
                    null));
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
                sourceResults,
                warnings,
                scannedAssetCount,
                scannedByteCount,
                performance,
                diagnostics,
                true);
        }

        private static void ObserveChangedSources(
            IEnumerable<AssetAnalysisState> sourceStates,
            ISet<string> changedSourcePaths)
        {
            foreach (AssetAnalysisState sourceState in sourceStates)
            {
                I18nSerializedAssetObservation observation = sourceState.Observation;
                if (observation.Stamp != I18nUsageSourceStamp.Capture(
                        observation.AbsolutePath,
                        observation.AbsolutePath + ".meta"))
                {
                    changedSourcePaths.Add(observation.AssetPath);
                }
            }
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

        private sealed class AssetAnalysisState
        {
            public AssetAnalysisState(
                I18nSerializedAssetSource source,
                I18nSerializedAssetObservation observation)
            {
                Source = source;
                Observation = observation;
            }

            public I18nSerializedAssetSource Source { get; }
            public I18nSerializedAssetObservation Observation { get; }
            public List<string> Warnings { get; } = new();
            public string AssetGuid { get; set; } = string.Empty;
            public string? Error { get; set; }
            public bool ContainsI18nData { get; set; }
            public I18nSerializedAssetModel? Model { get; set; }
        }

    }

}
