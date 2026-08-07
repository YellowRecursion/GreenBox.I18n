#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreenBox.I18n.Usage.Analysis;
using GreenBox.I18n.Usage.Cecil;
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
            out int reportedLocationCount,
            bool includeLocations = true)
        {
            var lines = new List<string>
            {
                $"IL usage scan completed in {result.ElapsedMilliseconds} ms. " +
                $"Scanned {result.ScannedAssemblyCount} player assembly(s) with Assets sources; " +
                $"{result.CandidateAssemblyCount} required Cecil analysis; " +
                $"{result.FailedSourceCount} failed; " +
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
            if (includeLocations)
            {
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
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Scan(
                CompilationPipeline.GetAssemblies(AssembliesType.Player),
                projectRoot,
                new I18nUsageScanProfiler());
        }

        /// <summary>
        /// Scans only player assemblies whose compiled output paths are specified.
        /// </summary>
        internal static I18nIlUsageScanResult Scan(IReadOnlyList<string> assemblyPaths)
        {
            if (assemblyPaths == null)
            {
                throw new ArgumentNullException(nameof(assemblyPaths));
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var requestedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assemblyPath in assemblyPaths)
            {
                if (!string.IsNullOrWhiteSpace(assemblyPath))
                {
                    requestedPaths.Add(I18nUsagePath.Resolve(assemblyPath, projectRoot));
                }
            }

            UnityCompilationAssembly[] assemblies = CompilationPipeline
                .GetAssemblies(AssembliesType.Player)
                .Where(assembly => requestedPaths.Contains(
                    I18nUsagePath.Resolve(assembly.outputPath, projectRoot)))
                .ToArray();
            return Scan(
                assemblies,
                projectRoot,
                new I18nUsageScanProfiler(false));
        }

        private static I18nIlUsageScanResult Scan(
            IEnumerable<UnityCompilationAssembly> assemblies,
            string projectRoot,
            I18nUsageScanProfiler profiler)
        {
            var sourceResults = new List<I18nIlUsageSourceScanResult>();
            var globalWarnings = new List<string>();

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
                    sourceResults.Add(new I18nIlUsageSourceScanResult(
                        assembly.name,
                        assemblyPath,
                        I18nUsageSourceScanStatus.Changed,
                        Array.Empty<I18nIlUsage>(),
                        new[]
                        {
                            $"The compiled DLL was not found at '{assemblyPath}'; " +
                            "the assembly may have changed while the scan was being prepared.",
                        },
                        null,
                        false,
                        null));
                    continue;
                }

                string symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
                I18nUsageSourceStamp initialStamp = I18nUsageSourceStamp.Capture(
                    assemblyPath,
                    symbolsPath);
                var sourceWarnings = new List<string>();
                bool mayContainEntryId;
                try
                {
                    mayContainEntryId = I18nIlAssemblyPrefilter.MayContainEntryId(assemblyPath);
                }
                catch (Exception exception)
                {
                    mayContainEntryId = true;
                    sourceWarnings.Add(
                        $"Prefilter failed for '{assembly.name}'; falling back to Cecil: " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }

                if (!mayContainEntryId)
                {
                    sourceResults.Add(CreateSourceResult(
                        assembly.name,
                        assemblyPath,
                        symbolsPath,
                        initialStamp,
                        Array.Empty<I18nIlUsage>(),
                        sourceWarnings,
                        null,
                        false,
                        null));
                    continue;
                }

                var assemblyUsages = new HashSet<I18nIlUsage>();
                I18nCecilAssemblyScanPerformance? cecilPerformance = null;
                string? error = null;
                try
                {
                    cecilPerformance = I18nCecilUsageScanner.Scan(
                        assembly.name,
                        assemblyPath,
                        assembly.allReferences,
                        projectRoot,
                        assemblyUsages,
                        profiler);
                }
                catch (Exception exception)
                {
                    error = $"{exception.GetType().Name}: {exception.Message}";
                }

                sourceResults.Add(CreateSourceResult(
                    assembly.name,
                    assemblyPath,
                    symbolsPath,
                    initialStamp,
                    assemblyUsages,
                    sourceWarnings,
                    error,
                    true,
                    cecilPerformance));

                profiler.Sample();
            }

            I18nUsageScanPerformance performance = profiler.Complete();
            return new I18nIlUsageScanResult(
                sourceResults,
                globalWarnings,
                performance);
        }

        private static I18nIlUsageSourceScanResult CreateSourceResult(
            string assemblyName,
            string assemblyPath,
            string symbolsPath,
            I18nUsageSourceStamp initialStamp,
            IEnumerable<I18nIlUsage> usages,
            IReadOnlyList<string> warnings,
            string? error,
            bool requiredCecilAnalysis,
            I18nCecilAssemblyScanPerformance? cecilPerformance)
        {
            I18nUsageSourceScanStatus status;
            var sourceWarnings = new List<string>(warnings);
            if (initialStamp != I18nUsageSourceStamp.Capture(assemblyPath, symbolsPath))
            {
                status = I18nUsageSourceScanStatus.Changed;
                sourceWarnings.Add(
                    $"'{Path.GetFileName(assemblyPath)}' changed during scanning; " +
                    "its result was discarded.");
            }
            else
            {
                status = error == null
                    ? I18nUsageSourceScanStatus.Success
                    : I18nUsageSourceScanStatus.Failed;
            }

            return new I18nIlUsageSourceScanResult(
                assemblyName,
                assemblyPath,
                status,
                status == I18nUsageSourceScanStatus.Success
                    ? usages
                        .Distinct()
                        .OrderBy(usage => usage.EntryId)
                        .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                        .ThenBy(usage => usage.Line)
                        .ToArray()
                    : Array.Empty<I18nIlUsage>(),
                sourceWarnings,
                error,
                requiredCecilAnalysis,
                cecilPerformance);
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

}
