#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Formats serialized usage results for Unity's rich-text console.
    /// </summary>
    internal static class I18nAssetUsageReportFormatter
    {
        private const int MaximumReportedLocationCount = 100;
        private const int MaximumReportedLocationsPerEntry = 10;

        internal static string Format(I18nAssetUsageScanResult result)
        {
            return Format(result, MaximumReportedLocationCount, out _);
        }

        internal static string Format(
            I18nAssetUsageScanResult result,
            int maximumReportedLocationCount,
            out int reportedLocationCount,
            bool includeLocations = true)
        {
            reportedLocationCount = 0;
            if (!result.IsForceText)
            {
                return "Asset usage scan was not run." + Environment.NewLine +
                       string.Join(Environment.NewLine, result.Warnings.Select(warning => $"  {warning}"));
            }

            var lines = new List<string>
            {
                $"Asset usage scan completed in {result.ElapsedMilliseconds} ms. " +
                $"Scanned {result.ScannedAssetCount} serialized asset file(s), " +
                $"{FormatByteCount(result.ScannedByteCount)} of source data; " +
                $"{result.MatchedAssetCount} contained I18nKey data; " +
                $"{result.FailedSourceCount} failed; " +
                $"found {result.Usages.Count} usage(s) across " +
                $"{result.Usages.Select(usage => usage.EntryId).Distinct().Count()} ID(s).",
            };

            I18nAssetUsageScanDiagnostics diagnostics = result.Diagnostics;
            lines.Add("Asset analysis breakdown:");
            lines.Add($"  Discover paths: {diagnostics.PathDiscoveryMilliseconds:0.0} ms");
            lines.Add($"  Read + marker prefilter: {diagnostics.MarkerPrefilterMilliseconds:0.0} ms");
            lines.Add($"  Resolve metadata: {diagnostics.AssetMetadataMilliseconds:0.0} ms");
            lines.Add($"  Parse matched YAML: {diagnostics.YamlParseMilliseconds:0.0} ms");
            lines.Add($"  Resolve contexts: {diagnostics.ContextResolutionMilliseconds:0.0} ms");
            lines.Add($"  Build results: {diagnostics.ResultBuildMilliseconds:0.0} ms");
            if (diagnostics.SlowestFiles.Count > 0)
            {
                lines.Add("  Slowest files:");
                lines.AddRange(diagnostics.SlowestFiles.Select(
                    file => $"    {file.AssetPath}: {file.ElapsedMilliseconds:0.0} ms"));
            }

            if (includeLocations)
            {
                foreach (IGrouping<long, I18nAssetUsage> group in result.Usages
                             .GroupBy(usage => usage.EntryId)
                             .OrderBy(group => group.Key))
                {
                    if (reportedLocationCount >= maximumReportedLocationCount)
                    {
                        break;
                    }

                    int locationLimit = Math.Min(
                        MaximumReportedLocationsPerEntry,
                        maximumReportedLocationCount - reportedLocationCount);
                    I18nAssetUsage[] reportedLocations = group.Take(locationLimit).ToArray();
                    lines.Add($"Entry {group.Key}: {group.Count()} serialized usage(s)");
                    lines.AddRange(reportedLocations.Select(FormatLocation));
                    reportedLocationCount += reportedLocations.Length;
                }

                int omittedLocationCount = result.Usages.Count - reportedLocationCount;
                if (omittedLocationCount > 0)
                {
                    lines.Add(
                        $"Asset output truncated: displayed {reportedLocationCount} of " +
                        $"{result.Usages.Count} locations; {omittedLocationCount} omitted.");
                }
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add("Warnings:");
                lines.AddRange(result.Warnings.Select(warning => $"  {warning}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatLocation(I18nAssetUsage usage)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, usage.AssetPath));
            string href = EscapeRichText(absolutePath);
            string label = EscapeRichText($"{usage.AssetPath}:{usage.Line}");
            string context = EscapeRichText(usage.FormatContext());
            return $"  <color=#40a0ff><a href=\"{href}\" line=\"{usage.Line}\">{label}</a></color> - {context}";
        }

        private static string EscapeRichText(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
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
