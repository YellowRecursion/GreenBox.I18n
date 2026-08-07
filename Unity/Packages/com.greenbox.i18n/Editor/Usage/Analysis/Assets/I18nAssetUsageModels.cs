#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenBox.I18n.Usage.Analysis
{
    internal sealed class I18nSerializedAssetSource
    {
        public I18nSerializedAssetSource(string absolutePath, string assetPath)
        {
            AbsolutePath = absolutePath ?? throw new ArgumentNullException(nameof(absolutePath));
            AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        }

        public string AbsolutePath { get; }

        public string AssetPath { get; }
    }

    internal sealed class I18nSerializedAssetObservation
    {
        public I18nSerializedAssetObservation(
            string absolutePath,
            string assetPath,
            I18nUsageSourceStamp stamp)
        {
            AbsolutePath = absolutePath;
            AssetPath = assetPath;
            Stamp = stamp;
        }

        public string AbsolutePath { get; }
        public string AssetPath { get; }
        public I18nUsageSourceStamp Stamp { get; }
    }

    internal sealed class I18nAssetUsageSourceScanResult
    {
        public I18nAssetUsageSourceScanResult(
            string sourceKey,
            string sourcePath,
            string assetGuid,
            I18nUsageSourceScanStatus status,
            IReadOnlyList<I18nAssetUsage> usages,
            IReadOnlyList<string> warnings,
            string? error,
            bool containsI18nData,
            I18nSerializedAssetObservation? observation)
        {
            SourceKey = sourceKey;
            SourcePath = sourcePath;
            AssetGuid = assetGuid;
            Status = status;
            Usages = new List<I18nAssetUsage>(usages).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            Error = error;
            ContainsI18nData = containsI18nData;
            Observation = observation;
        }

        public string SourceKey { get; }
        public string SourcePath { get; }
        public string AssetGuid { get; }
        public I18nUsageSourceScanStatus Status { get; }
        public IReadOnlyList<I18nAssetUsage> Usages { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string? Error { get; }
        public bool ContainsI18nData { get; }
        public I18nSerializedAssetObservation? Observation { get; }

        public I18nAssetUsageSourceScanResult With(
            string? assetGuid = null,
            I18nUsageSourceScanStatus? status = null,
            IReadOnlyList<I18nAssetUsage>? usages = null,
            IReadOnlyList<string>? warnings = null,
            string? error = null)
        {
            return new I18nAssetUsageSourceScanResult(
                SourceKey,
                SourcePath,
                assetGuid ?? AssetGuid,
                status ?? Status,
                usages ?? Usages,
                warnings ?? Warnings,
                error ?? Error,
                ContainsI18nData,
                Observation);
        }
    }

    internal sealed class I18nAssetUsageScanResult
    {
        public I18nAssetUsageScanResult(
            IReadOnlyList<I18nAssetUsageSourceScanResult> sources,
            IReadOnlyList<string> globalWarnings,
            int scannedAssetCount,
            long scannedByteCount,
            I18nUsageScanPerformance performance,
            I18nAssetUsageScanDiagnostics diagnostics,
            bool isForceText)
        {
            Sources = new List<I18nAssetUsageSourceScanResult>(sources).AsReadOnly();
            GlobalWarnings = new List<string>(globalWarnings).AsReadOnly();
            ScannedAssetCount = scannedAssetCount;
            ScannedByteCount = scannedByteCount;
            Performance = performance;
            Diagnostics = diagnostics;
            IsForceText = isForceText;

            Usages = Sources
                .Where(source => source.Status == I18nUsageSourceScanStatus.Success)
                .SelectMany(source => source.Usages)
                .ToArray();
            Warnings = GlobalWarnings
                .Concat(Sources.SelectMany(source => source.Warnings))
                .Concat(Sources
                    .Where(source => source.Error != null)
                    .Select(source => $"{source.SourceKey}: {source.Error}"))
                .ToArray();
            ChangedSourcePaths = Sources
                .Where(source => source.Status == I18nUsageSourceScanStatus.Changed)
                .Select(source => source.SourceKey)
                .ToArray();
        }

        public IReadOnlyList<I18nAssetUsageSourceScanResult> Sources { get; }
        public IReadOnlyList<string> GlobalWarnings { get; }
        public IReadOnlyList<I18nAssetUsage> Usages { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int ScannedAssetCount { get; }
        public int MatchedAssetCount => Sources.Count(source => source.ContainsI18nData);
        public int FailedSourceCount => Sources.Count(
            source => source.Status == I18nUsageSourceScanStatus.Failed);
        public long ScannedByteCount { get; }
        public I18nUsageScanPerformance Performance { get; }
        public I18nAssetUsageScanDiagnostics Diagnostics { get; }
        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;
        public bool IsForceText { get; }
        public IReadOnlyList<string> ChangedSourcePaths { get; }
        public bool IsStable => ChangedSourcePaths.Count == 0;

        public I18nAssetUsageScanResult MarkChangedSources(IEnumerable<string> sourceKeys)
        {
            var changedSourceKeys = new HashSet<string>(
                sourceKeys,
                StringComparer.OrdinalIgnoreCase);
            if (changedSourceKeys.Count == 0)
            {
                return this;
            }

            var sources = Sources
                .Select(source => changedSourceKeys.Remove(source.SourceKey)
                    ? source.With(
                        status: I18nUsageSourceScanStatus.Changed,
                        usages: Array.Empty<I18nAssetUsage>(),
                        warnings: source.Warnings.Concat(new[]
                        {
                            $"'{source.SourceKey}' changed while the scan was running; " +
                            "its result was discarded.",
                        }).ToArray())
                    : source)
                .ToList();
            sources.AddRange(changedSourceKeys.Select(sourceKey =>
                new I18nAssetUsageSourceScanResult(
                    sourceKey,
                    string.Empty,
                    string.Empty,
                    I18nUsageSourceScanStatus.Changed,
                    Array.Empty<I18nAssetUsage>(),
                    new[]
                    {
                        $"'{sourceKey}' changed while the scan was running and was not analyzed.",
                    },
                    null,
                    false,
                    null)));
            return new I18nAssetUsageScanResult(
                sources,
                GlobalWarnings,
                ScannedAssetCount,
                ScannedByteCount,
                Performance,
                Diagnostics,
                IsForceText);
        }
    }

    internal sealed class I18nAssetUsageScanDiagnostics
    {
        public static I18nAssetUsageScanDiagnostics Empty { get; } = new(
            0, 0, 0, 0, 0, 0, Array.Empty<I18nAssetFileScanPerformance>());

        public I18nAssetUsageScanDiagnostics(
            double pathDiscoveryMilliseconds,
            double markerPrefilterMilliseconds,
            double assetMetadataMilliseconds,
            double yamlParseMilliseconds,
            double contextResolutionMilliseconds,
            double resultBuildMilliseconds,
            IReadOnlyList<I18nAssetFileScanPerformance> slowestFiles)
        {
            PathDiscoveryMilliseconds = pathDiscoveryMilliseconds;
            MarkerPrefilterMilliseconds = markerPrefilterMilliseconds;
            AssetMetadataMilliseconds = assetMetadataMilliseconds;
            YamlParseMilliseconds = yamlParseMilliseconds;
            ContextResolutionMilliseconds = contextResolutionMilliseconds;
            ResultBuildMilliseconds = resultBuildMilliseconds;
            SlowestFiles = new List<I18nAssetFileScanPerformance>(slowestFiles).AsReadOnly();
        }

        public double PathDiscoveryMilliseconds { get; }
        public double MarkerPrefilterMilliseconds { get; }
        public double AssetMetadataMilliseconds { get; }
        public double YamlParseMilliseconds { get; }
        public double ContextResolutionMilliseconds { get; }
        public double ResultBuildMilliseconds { get; }
        public IReadOnlyList<I18nAssetFileScanPerformance> SlowestFiles { get; }
    }

    internal sealed class I18nAssetFileScanPerformance
    {
        public I18nAssetFileScanPerformance(string assetPath, double elapsedMilliseconds)
        {
            AssetPath = assetPath;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public string AssetPath { get; }
        public double ElapsedMilliseconds { get; }
    }

    internal sealed class I18nAssetUsage
    {
        public I18nAssetUsage(
            long entryId,
            string assetGuid,
            string assetPath,
            long assetLocalId,
            long gameObjectLocalId,
            string objectPath,
            string componentType,
            string scriptGuid,
            string propertyPath,
            int line,
            bool isPrefabOverride,
            string targetAssetGuid,
            string targetAssetPath,
            long targetLocalId)
        {
            EntryId = entryId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            AssetLocalId = assetLocalId;
            GameObjectLocalId = gameObjectLocalId;
            ObjectPath = objectPath;
            ComponentType = componentType;
            ScriptGuid = scriptGuid;
            PropertyPath = propertyPath;
            Line = line;
            IsPrefabOverride = isPrefabOverride;
            TargetAssetGuid = targetAssetGuid;
            TargetAssetPath = targetAssetPath;
            TargetLocalId = targetLocalId;
        }

        public long EntryId { get; }
        public string AssetGuid { get; }
        public string AssetPath { get; }
        public long AssetLocalId { get; }
        public long GameObjectLocalId { get; }
        public string ObjectPath { get; }
        public string ComponentType { get; }
        public string ScriptGuid { get; }
        public string PropertyPath { get; }
        public int Line { get; }
        public bool IsPrefabOverride { get; }
        public string TargetAssetGuid { get; }
        public string TargetAssetPath { get; }
        public long TargetLocalId { get; }

        public I18nAssetUsage WithComponentType(string componentType)
        {
            return new I18nAssetUsage(
                EntryId,
                AssetGuid,
                AssetPath,
                AssetLocalId,
                GameObjectLocalId,
                ObjectPath,
                componentType,
                ScriptGuid,
                PropertyPath,
                Line,
                IsPrefabOverride,
                TargetAssetGuid,
                TargetAssetPath,
                TargetLocalId);
        }

        public string FormatContext()
        {
            var parts = new List<string>();
            if (IsPrefabOverride)
            {
                parts.Add(TargetAssetPath.Length > 0
                    ? $"Prefab override -> {TargetAssetPath}"
                    : "Prefab override");
            }

            if (ObjectPath.Length > 0)
            {
                parts.Add(ObjectPath);
            }

            if (ComponentType.Length > 0)
            {
                parts.Add(ComponentType);
            }

            parts.Add(PropertyPath);
            return string.Join(" / ", parts);
        }
    }
}
