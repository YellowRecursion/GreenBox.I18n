#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Prepares Unity-owned inputs for serialized asset analysis and enriches its pure result.
    /// </summary>
    internal static class I18nAssetUsageScanner
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",
            ".prefab",
            ".unity",
        };

        internal static bool IsSupportedAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   SupportedExtensions.Contains(Path.GetExtension(path));
        }

        internal static I18nAssetUsageScanResult Scan()
        {
            var profiler = new I18nUsageScanProfiler();
            var warnings = new List<string>();
            if (!TryValidateSerializationMode(profiler, warnings, out I18nAssetUsageScanResult? failure))
            {
                return failure!;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            long pathDiscoveryStart = Stopwatch.GetTimestamp();
            I18nSerializedAssetSource[] sources = Directory
                .EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories)
                .Where(IsSupportedAssetPath)
                .Select(path => new I18nSerializedAssetSource(
                    path,
                    ToAssetPath(path, projectRoot)))
                .ToArray();
            double pathDiscoveryMilliseconds = ToMilliseconds(
                Stopwatch.GetTimestamp() - pathDiscoveryStart);
            return EnrichComponentTypes(I18nAssetUsageAnalyzer.Analyze(
                sources,
                profiler,
                warnings,
                Array.Empty<string>()), profiler, pathDiscoveryMilliseconds, sources);
        }

        /// <summary>
        /// Scans only the specified Unity asset paths without producing console output.
        /// </summary>
        internal static I18nAssetUsageScanResult Scan(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null)
            {
                throw new ArgumentNullException(nameof(assetPaths));
            }

            var profiler = new I18nUsageScanProfiler(false);
            var warnings = new List<string>();
            if (!TryValidateSerializationMode(profiler, warnings, out I18nAssetUsageScanResult? failure))
            {
                return failure!;
            }

            var changedSourcePaths = new List<string>();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return EnrichComponentTypes(I18nAssetUsageAnalyzer.Analyze(
                ResolveSources(assetPaths, projectRoot, warnings, changedSourcePaths),
                profiler,
                warnings,
                changedSourcePaths), profiler);
        }

        private static bool TryValidateSerializationMode(
            I18nUsageScanProfiler profiler,
            ICollection<string> warnings,
            out I18nAssetUsageScanResult? failure)
        {
            if (EditorSettings.serializationMode == SerializationMode.ForceText)
            {
                failure = null;
                return true;
            }

            warnings.Add(
                "Asset usage scanning requires Edit > Project Settings > Editor > " +
                "Asset Serialization > Mode to be set to Force Text.");
            failure = new I18nAssetUsageScanResult(
                Array.Empty<I18nAssetUsageSourceScanResult>(),
                warnings.ToArray(),
                0,
                0,
                profiler.Complete(),
                I18nAssetUsageScanDiagnostics.Empty,
                false);
            return false;
        }

        private static IEnumerable<I18nSerializedAssetSource> ResolveSources(
            IEnumerable<string> assetPaths,
            string projectRoot,
            ICollection<string> warnings,
            ICollection<string> changedSourcePaths)
        {
            var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsPrefix = assetsRoot + Path.DirectorySeparatorChar;

            foreach (string path in assetPaths)
            {
                if (!IsSupportedAssetPath(path))
                {
                    continue;
                }

                string absolutePath;
                try
                {
                    absolutePath = Path.GetFullPath(
                        Path.IsPathRooted(path)
                            ? path
                            : Path.Combine(projectRoot, path));
                }
                catch (Exception exception)
                {
                    warnings.Add($"Skipped invalid asset path '{path}': {exception.Message}");
                    continue;
                }

                if (!absolutePath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Skipped path outside Assets: '{path}'.");
                    continue;
                }

                string assetPath = ToAssetPath(absolutePath, projectRoot);
                if (!File.Exists(absolutePath))
                {
                    warnings.Add($"Skipped missing asset: '{assetPath}'.");
                    changedSourcePaths.Add(assetPath);
                    continue;
                }

                if (resolvedPaths.Add(absolutePath))
                {
                    yield return new I18nSerializedAssetSource(absolutePath, assetPath);
                }
            }
        }

        private static I18nAssetUsageScanResult EnrichComponentTypes(
            I18nAssetUsageScanResult result,
            I18nUsageScanProfiler profiler,
            double externalPathDiscoveryMilliseconds = 0,
            IReadOnlyList<I18nSerializedAssetSource>? fullScanSources = null)
        {
            long metadataStart = Stopwatch.GetTimestamp();
            var originalAssetGuidsByPath = result.Sources.ToDictionary(
                source => source.SourceKey,
                source => source.AssetGuid,
                StringComparer.OrdinalIgnoreCase);
            var assetGuidsByPath = result.Sources.ToDictionary(
                source => source.SourceKey,
                source => AssetDatabase.AssetPathToGUID(source.SourceKey),
                StringComparer.OrdinalIgnoreCase);
            var typesByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string scriptGuid in result.Usages
                         .Where(RequiresScriptTypeResolution)
                         .Select(usage => usage.ScriptGuid)
                         .Where(guid => guid.Length > 0)
                         .Distinct(StringComparer.Ordinal))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                typesByGuid[scriptGuid] = scriptPath.Length > 0
                    ? Path.GetFileNameWithoutExtension(scriptPath)
                    : string.Empty;
            }

            var sources = result.Sources
                .Select(source => source.With(
                    assetGuid: assetGuidsByPath.TryGetValue(source.SourceKey, out string assetGuid) &&
                               assetGuid.Length > 0
                        ? assetGuid
                        : source.AssetGuid,
                    usages: source.Usages
                        .Select(usage => RequiresScriptTypeResolution(usage) &&
                                         typesByGuid.TryGetValue(usage.ScriptGuid, out string typeName) &&
                                         typeName.Length > 0
                            ? usage.WithComponentType(typeName)
                            : usage)
                        .ToArray()))
                .ToList();
            double metadataMilliseconds = ToMilliseconds(Stopwatch.GetTimestamp() - metadataStart);
            for (int index = 0; index < sources.Count; index++)
            {
                I18nAssetUsageSourceScanResult source = sources[index];
                I18nSerializedAssetObservation? observation = source.Observation;
                if (observation == null ||
                    observation.Stamp == I18nUsageSourceStamp.Capture(
                        observation.AbsolutePath,
                        observation.AbsolutePath + ".meta"))
                {
                    continue;
                }

                sources[index] = MarkChanged(
                    source,
                    $"'{source.SourceKey}' changed during Unity metadata resolution; " +
                    "its result was discarded.");
            }

            if (fullScanSources != null)
            {
                long pathValidationStart = Stopwatch.GetTimestamp();
                var initialPaths = new HashSet<string>(
                    fullScanSources.Select(source => source.AssetPath),
                    StringComparer.OrdinalIgnoreCase);
                string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
                var currentPaths = new HashSet<string>(
                    Directory
                        .EnumerateFiles(Application.dataPath, "*", SearchOption.AllDirectories)
                        .Where(IsSupportedAssetPath)
                        .Select(path => ToAssetPath(path, projectRoot)),
                    StringComparer.OrdinalIgnoreCase);
                string[] changedAssetSet = initialPaths
                    .Except(currentPaths, StringComparer.OrdinalIgnoreCase)
                    .Concat(currentPaths.Except(initialPaths, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                externalPathDiscoveryMilliseconds += ToMilliseconds(
                    Stopwatch.GetTimestamp() - pathValidationStart);
                if (changedAssetSet.Length > 0)
                {
                    var sourcesByKey = sources.ToDictionary(
                        source => source.SourceKey,
                        StringComparer.OrdinalIgnoreCase);
                    foreach (string changedPath in changedAssetSet)
                    {
                        if (sourcesByKey.TryGetValue(changedPath, out I18nAssetUsageSourceScanResult source))
                        {
                            int sourceIndex = sources.FindIndex(item => string.Equals(
                                item.SourceKey,
                                changedPath,
                                StringComparison.OrdinalIgnoreCase));
                            sources[sourceIndex] = MarkChanged(
                                source,
                                $"'{changedPath}' was removed during the full scan; " +
                                "its result was discarded.");
                            continue;
                        }

                        sources.Add(new I18nAssetUsageSourceScanResult(
                            changedPath,
                            Path.GetFullPath(Path.Combine(projectRoot, changedPath)),
                            AssetDatabase.AssetPathToGUID(changedPath),
                            I18nUsageSourceScanStatus.Changed,
                            Array.Empty<I18nAssetUsage>(),
                            new[]
                            {
                                $"'{changedPath}' was added during the full scan and was not analyzed.",
                            },
                            null,
                            false,
                            null));
                    }
                }
            }

            MarkChangedPrefabDependents(sources, originalAssetGuidsByPath);

            var diagnostics = new I18nAssetUsageScanDiagnostics(
                result.Diagnostics.PathDiscoveryMilliseconds + externalPathDiscoveryMilliseconds,
                result.Diagnostics.MarkerPrefilterMilliseconds,
                result.Diagnostics.AssetMetadataMilliseconds + metadataMilliseconds,
                result.Diagnostics.YamlParseMilliseconds,
                result.Diagnostics.ContextResolutionMilliseconds,
                result.Diagnostics.ResultBuildMilliseconds,
                result.Diagnostics.SlowestFiles);
            return new I18nAssetUsageScanResult(
                sources,
                result.GlobalWarnings,
                result.ScannedAssetCount,
                result.ScannedByteCount,
                profiler.Complete(),
                diagnostics,
                result.IsForceText);
        }

        private static I18nAssetUsageSourceScanResult MarkChanged(
            I18nAssetUsageSourceScanResult source,
            string warning)
        {
            return source.With(
                status: I18nUsageSourceScanStatus.Changed,
                usages: Array.Empty<I18nAssetUsage>(),
                warnings: source.Warnings.Concat(new[] { warning }).ToArray());
        }

        private static void MarkChangedPrefabDependents(
            IList<I18nAssetUsageSourceScanResult> sources,
            IReadOnlyDictionary<string, string> originalAssetGuidsByPath)
        {
            var changedAssetGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (I18nAssetUsageSourceScanResult source in sources.Where(
                         item => item.Status == I18nUsageSourceScanStatus.Changed))
            {
                AddSourceGuids(source);
            }

            if (changedAssetGuids.Count == 0)
            {
                return;
            }

            bool markedDependent;
            do
            {
                markedDependent = false;
                for (int index = 0; index < sources.Count; index++)
                {
                    I18nAssetUsageSourceScanResult source = sources[index];
                    if (source.Status == I18nUsageSourceScanStatus.Changed ||
                        !source.Usages.Any(usage => usage.IsPrefabOverride &&
                                                    changedAssetGuids.Contains(usage.TargetAssetGuid)))
                    {
                        continue;
                    }

                    sources[index] = MarkChanged(
                        source,
                        $"'{source.SourceKey}' depends on a prefab that changed during scanning; " +
                        "its result was discarded.");
                    AddSourceGuids(source);
                    markedDependent = true;
                }
            } while (markedDependent);

            void AddSourceGuids(I18nAssetUsageSourceScanResult source)
            {
                if (source.AssetGuid.Length > 0)
                {
                    changedAssetGuids.Add(source.AssetGuid);
                }

                if (originalAssetGuidsByPath.TryGetValue(source.SourceKey, out string originalGuid) &&
                    originalGuid.Length > 0)
                {
                    changedAssetGuids.Add(originalGuid);
                }
            }
        }

        private static bool RequiresScriptTypeResolution(I18nAssetUsage usage)
        {
            return usage.ScriptGuid.Length > 0 &&
                   (usage.ComponentType.Length == 0 || usage.ComponentType == "MonoBehaviour");
        }

        private static string ToAssetPath(string absolutePath, string projectRoot)
        {
            string normalizedPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string normalizedRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(normalizedRoot.Length + 1)
                : normalizedPath;
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

    }
}
