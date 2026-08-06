#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;
using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Finds self-identifying entry IDs embedded as 64-bit constants in Unity player assemblies.
    /// </summary>
    internal static class I18nIlUsageScanner
    {
        private const int MaximumReportedLocationCount = 100;
        private const int MaximumReportedLocationsPerEntry = 10;

        internal static string FormatReport(I18nIlUsageScanResult result)
        {
            return FormatReport(result, MaximumReportedLocationCount, out _);
        }

        internal static string FormatReport(
            I18nIlUsageScanResult result,
            int maximumReportedLocationCount,
            out int reportedLocationCount)
        {
            var lines = new List<string>
            {
                $"[GreenBox I18n] IL usage scan completed in {result.ElapsedMilliseconds} ms. " +
                $"Scanned {result.ScannedAssemblyCount} player assembly(s) with Assets sources; " +
                $"{result.CandidateAssemblyCount} required Cecil analysis; " +
                $"found {result.Usages.Count} usage(s) across " +
                $"{result.Usages.Select(usage => usage.EntryId).Distinct().Count()} ID(s).",
            };

            if (result.CecilPerformance.Count > 0)
            {
                lines.Add("Cecil analysis breakdown:");
                foreach (I18nCecilAssemblyScanPerformance performance in result.CecilPerformance
                             .OrderByDescending(item => item.TotalMilliseconds))
                {
                    string readStage = performance.HasSymbols ? "read DLL + PDB" : "read DLL";
                    lines.Add(
                        $"  {performance.AssemblyName}: {performance.TotalMilliseconds:0.0} ms total " +
                        $"(resolver {performance.ResolverMilliseconds:0.0} ms; " +
                        $"{readStage} {performance.ReadMilliseconds:0.0} ms; " +
                        $"traverse IL {performance.TraversalMilliseconds:0.0} ms; " +
                        $"resolve locations {performance.LocationResolutionMilliseconds:0.0} ms)");
                }
            }

            reportedLocationCount = 0;
            foreach (IGrouping<long, I18nIlUsage> group in result.Usages
                         .GroupBy(usage => usage.EntryId)
                         .OrderBy(group => group.Key))
            {
                if (reportedLocationCount >= maximumReportedLocationCount)
                {
                    break;
                }

                I18nIlUsage[] orderedLocations = group
                    .OrderBy(usage => usage.AssetPath, StringComparer.Ordinal)
                    .ThenBy(usage => usage.Line)
                    .ToArray();
                int locationLimit = Math.Min(
                    MaximumReportedLocationsPerEntry,
                    maximumReportedLocationCount - reportedLocationCount);
                I18nIlUsage[] reportedLocations = orderedLocations
                    .Take(locationLimit)
                    .ToArray();

                lines.Add($"Entry {group.Key}: {group.Count()} IL usage(s)");
                lines.AddRange(reportedLocations.Select(FormatLocation));
                reportedLocationCount += reportedLocations.Length;
            }

            int omittedLocationCount = result.Usages.Count - reportedLocationCount;
            if (omittedLocationCount > 0)
            {
                lines.Add(
                    $"Output truncated: displayed {reportedLocationCount} of {result.Usages.Count} locations; " +
                    $"{omittedLocationCount} omitted.");
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add("Warnings:");
                lines.AddRange(result.Warnings.Select(warning => $"  {warning}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        internal static I18nIlUsageScanResult Scan()
        {
            var profiler = new I18nUsageScanProfiler();
            var usages = new HashSet<I18nIlUsage>();
            var warnings = new List<string>();
            var cecilPerformance = new List<I18nCecilAssemblyScanPerformance>();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            int scannedAssemblyCount = 0;
            int candidateAssemblyCount = 0;

            UnityCompilationAssembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (UnityCompilationAssembly assembly in assemblies)
            {
                if (!assembly.sourceFiles.Any(
                        sourceFile => I18nUsagePath.IsAssetPath(sourceFile, projectRoot)))
                {
                    continue;
                }

                string assemblyPath = I18nUsagePath.Resolve(assembly.outputPath, projectRoot);
                if (!File.Exists(assemblyPath))
                {
                    warnings.Add($"Skipped '{assembly.name}' because its compiled DLL was not found at '{assemblyPath}'.");
                    continue;
                }

                scannedAssemblyCount++;
                bool mayContainEntryId;
                try
                {
                    mayContainEntryId = I18nIlAssemblyPrefilter.MayContainEntryId(assemblyPath);
                }
                catch (Exception exception)
                {
                    mayContainEntryId = true;
                    warnings.Add(
                        $"Prefilter failed for '{assembly.name}'; falling back to Cecil: " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }

                if (!mayContainEntryId)
                {
                    continue;
                }

                candidateAssemblyCount++;
                try
                {
                    cecilPerformance.Add(I18nCecilUsageScanner.Scan(
                        assembly.name,
                        assemblyPath,
                        assembly.allReferences,
                        projectRoot,
                        usages,
                        profiler));
                }
                catch (Exception exception)
                {
                    warnings.Add(
                        $"Skipped '{assembly.name}': {exception.GetType().Name}: {exception.Message}");
                }

                profiler.Sample();
            }

            I18nUsageScanPerformance performance = profiler.Complete();
            return new I18nIlUsageScanResult(
                usages
                    .OrderBy(usage => usage.EntryId)
                    .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                    .ThenBy(usage => usage.Line)
                    .ToArray(),
                warnings,
                scannedAssemblyCount,
                candidateAssemblyCount,
                cecilPerformance,
                performance);
        }

        private static string FormatLocation(I18nIlUsage usage)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, usage.AssetPath));
            string href = EscapeRichTextAttribute(absolutePath);
            string label = EscapeRichTextAttribute($"{usage.AssetPath}:{usage.Line}");
            return $"  <color=#40a0ff><a href=\"{href}\" line=\"{usage.Line}\">{label}</a></color>";
        }

        private static string EscapeRichTextAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }

    internal sealed class I18nIlUsageScanResult
    {
        public I18nIlUsageScanResult(
            IReadOnlyList<I18nIlUsage> usages,
            IReadOnlyList<string> warnings,
            int scannedAssemblyCount,
            int candidateAssemblyCount,
            IReadOnlyList<I18nCecilAssemblyScanPerformance> cecilPerformance,
            I18nUsageScanPerformance performance)
        {
            Usages = usages;
            Warnings = warnings;
            ScannedAssemblyCount = scannedAssemblyCount;
            CandidateAssemblyCount = candidateAssemblyCount;
            CecilPerformance = cecilPerformance;
            Performance = performance;
        }

        public IReadOnlyList<I18nIlUsage> Usages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int ScannedAssemblyCount { get; }

        public int CandidateAssemblyCount { get; }

        public IReadOnlyList<I18nCecilAssemblyScanPerformance> CecilPerformance { get; }

        public I18nUsageScanPerformance Performance { get; }

        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;
    }

    internal sealed class I18nIlUsage : IEquatable<I18nIlUsage>
    {
        public I18nIlUsage(long entryId, string assetPath, int line)
        {
            EntryId = entryId;
            AssetPath = assetPath;
            Line = line;
        }

        public long EntryId { get; }

        public string AssetPath { get; }

        public int Line { get; }

        public bool Equals(I18nIlUsage? other)
        {
            return other != null &&
                   EntryId == other.EntryId &&
                   Line == other.Line &&
                   string.Equals(AssetPath, other.AssetPath, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as I18nIlUsage);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = EntryId.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(AssetPath);
                hashCode = (hashCode * 397) ^ Line;
                return hashCode;
            }
        }
    }
}
