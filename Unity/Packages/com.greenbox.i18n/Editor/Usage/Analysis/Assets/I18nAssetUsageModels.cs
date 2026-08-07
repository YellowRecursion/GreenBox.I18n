#nullable enable

using System;
using System.Collections.Generic;

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

    internal sealed class I18nAssetUsageScanResult
    {
        public I18nAssetUsageScanResult(
            IReadOnlyList<I18nAssetUsage> usages,
            IReadOnlyList<string> warnings,
            int scannedAssetCount,
            int matchedAssetCount,
            long scannedByteCount,
            I18nUsageScanPerformance performance,
            I18nAssetUsageScanDiagnostics diagnostics,
            bool isForceText,
            IReadOnlyList<string> changedSourcePaths,
            IReadOnlyList<I18nSerializedAssetObservation> sourceObservations)
        {
            Usages = new List<I18nAssetUsage>(usages).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            ScannedAssetCount = scannedAssetCount;
            MatchedAssetCount = matchedAssetCount;
            ScannedByteCount = scannedByteCount;
            Performance = performance;
            Diagnostics = diagnostics;
            IsForceText = isForceText;
            ChangedSourcePaths = new List<string>(changedSourcePaths).AsReadOnly();
            SourceObservations = new List<I18nSerializedAssetObservation>(sourceObservations).AsReadOnly();
        }

        public IReadOnlyList<I18nAssetUsage> Usages { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int ScannedAssetCount { get; }
        public int MatchedAssetCount { get; }
        public long ScannedByteCount { get; }
        public I18nUsageScanPerformance Performance { get; }
        public I18nAssetUsageScanDiagnostics Diagnostics { get; }
        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;
        public bool IsForceText { get; }
        public IReadOnlyList<string> ChangedSourcePaths { get; }
        public IReadOnlyList<I18nSerializedAssetObservation> SourceObservations { get; }
        public bool IsStable => ChangedSourcePaths.Count == 0;
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
