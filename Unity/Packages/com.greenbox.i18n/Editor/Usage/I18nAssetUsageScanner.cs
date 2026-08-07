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
                Array.Empty<I18nAssetUsage>(),
                warnings.ToArray(),
                0,
                0,
                0,
                profiler.Complete(),
                I18nAssetUsageScanDiagnostics.Empty,
                false,
                Array.Empty<string>(),
                Array.Empty<I18nSerializedAssetObservation>());
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

            I18nAssetUsage[] usages = result.Usages
                .Select(usage => RequiresScriptTypeResolution(usage) &&
                                 typesByGuid.TryGetValue(usage.ScriptGuid, out string typeName) &&
                                 typeName.Length > 0
                    ? usage.WithComponentType(typeName)
                    : usage)
                .ToArray();
            double metadataMilliseconds = ToMilliseconds(Stopwatch.GetTimestamp() - metadataStart);
            string[] changedSourcePaths = result.ChangedSourcePaths
                .Concat(result.SourceObservations
                    .Where(observation => observation.Stamp != I18nUsageSourceStamp.Capture(
                        observation.AbsolutePath,
                        observation.AbsolutePath + ".meta"))
                    .Select(observation => observation.AssetPath))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var warnings = new List<string>(result.Warnings);
            foreach (string changedSourcePath in changedSourcePaths.Except(
                         result.ChangedSourcePaths,
                         StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"'{changedSourcePath}' changed during Unity metadata resolution; " +
                    "the scan result will not be published.");
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
                    changedSourcePaths = changedSourcePaths
                        .Concat(changedAssetSet)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();
                    warnings.Add(
                        $"The serialized asset set changed by {changedAssetSet.Length} file(s) " +
                        "during the full scan; the result will not be published.");
                }
            }

            var diagnostics = new I18nAssetUsageScanDiagnostics(
                result.Diagnostics.PathDiscoveryMilliseconds + externalPathDiscoveryMilliseconds,
                result.Diagnostics.MarkerPrefilterMilliseconds,
                result.Diagnostics.AssetMetadataMilliseconds + metadataMilliseconds,
                result.Diagnostics.YamlParseMilliseconds,
                result.Diagnostics.ContextResolutionMilliseconds,
                result.Diagnostics.ResultBuildMilliseconds,
                result.Diagnostics.SlowestFiles);
            return new I18nAssetUsageScanResult(
                usages,
                warnings,
                result.ScannedAssetCount,
                result.MatchedAssetCount,
                result.ScannedByteCount,
                profiler.Complete(),
                diagnostics,
                result.IsForceText,
                changedSourcePaths,
                result.SourceObservations);
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
