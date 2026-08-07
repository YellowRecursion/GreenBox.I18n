#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GreenBox.I18n.Usage.Analysis
{
    internal sealed class I18nIlUsageSourceScanResult
    {
        public I18nIlUsageSourceScanResult(
            string sourceKey,
            string sourcePath,
            I18nUsageSourceScanStatus status,
            IReadOnlyList<I18nIlUsage> usages,
            IReadOnlyList<string> warnings,
            string? error,
            bool requiredCecilAnalysis,
            I18nCecilAssemblyScanPerformance? cecilPerformance)
        {
            SourceKey = sourceKey;
            SourcePath = sourcePath;
            Status = status;
            Usages = new List<I18nIlUsage>(usages).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            Error = error;
            RequiredCecilAnalysis = requiredCecilAnalysis;
            CecilPerformance = cecilPerformance;
        }

        public string SourceKey { get; }
        public string SourcePath { get; }
        public I18nUsageSourceScanStatus Status { get; }
        public IReadOnlyList<I18nIlUsage> Usages { get; }
        public IReadOnlyList<string> Warnings { get; }
        public string? Error { get; }
        public bool RequiredCecilAnalysis { get; }
        public I18nCecilAssemblyScanPerformance? CecilPerformance { get; }

        public I18nIlUsageSourceScanResult MarkChanged(string warning)
        {
            return new I18nIlUsageSourceScanResult(
                SourceKey,
                SourcePath,
                I18nUsageSourceScanStatus.Changed,
                Array.Empty<I18nIlUsage>(),
                Warnings.Concat(new[] { warning }).ToArray(),
                Error,
                RequiredCecilAnalysis,
                CecilPerformance);
        }
    }

    internal sealed class I18nIlUsageScanResult
    {
        public I18nIlUsageScanResult(
            IReadOnlyList<I18nIlUsageSourceScanResult> sources,
            IReadOnlyList<string> globalWarnings,
            I18nUsageScanPerformance performance)
        {
            Sources = new List<I18nIlUsageSourceScanResult>(sources).AsReadOnly();
            GlobalWarnings = new List<string>(globalWarnings).AsReadOnly();
            Performance = performance;

            Usages = Sources
                .Where(source => source.Status == I18nUsageSourceScanStatus.Success)
                .SelectMany(source => source.Usages)
                .Distinct()
                .OrderBy(usage => usage.EntryId)
                .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                .ThenBy(usage => usage.Line)
                .ToArray();
            Warnings = GlobalWarnings
                .Concat(Sources.SelectMany(source => source.Warnings))
                .Concat(Sources
                    .Where(source => source.Error != null)
                    .Select(source => $"{source.SourceKey}: {source.Error}"))
                .ToArray();
            CecilPerformance = Sources
                .Select(source => source.CecilPerformance)
                .Where(item => item != null)
                .Cast<I18nCecilAssemblyScanPerformance>()
                .ToArray();
            ChangedSourcePaths = Sources
                .Where(source => source.Status == I18nUsageSourceScanStatus.Changed)
                .Select(source => source.SourcePath)
                .ToArray();
        }

        public IReadOnlyList<I18nIlUsageSourceScanResult> Sources { get; }

        public IReadOnlyList<string> GlobalWarnings { get; }

        public IReadOnlyList<I18nIlUsage> Usages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int ScannedAssemblyCount => Sources.Count;

        public int CandidateAssemblyCount => Sources.Count(source => source.RequiredCecilAnalysis);

        public int FailedSourceCount => Sources.Count(
            source => source.Status == I18nUsageSourceScanStatus.Failed);

        public IReadOnlyList<I18nCecilAssemblyScanPerformance> CecilPerformance { get; }

        public I18nUsageScanPerformance Performance { get; }

        public IReadOnlyList<string> ChangedSourcePaths { get; }

        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;

        public bool IsStable => ChangedSourcePaths.Count == 0;

        public I18nIlUsageScanResult MarkChangedSources(IEnumerable<string> sourcePaths)
        {
            var changedSourcePaths = new HashSet<string>(
                sourcePaths,
                StringComparer.OrdinalIgnoreCase);
            if (changedSourcePaths.Count == 0)
            {
                return this;
            }

            I18nIlUsageSourceScanResult[] sources = Sources
                .Select(source => changedSourcePaths.Remove(source.SourcePath)
                    ? source.MarkChanged(
                        $"'{source.SourceKey}' changed while the scan was running; " +
                        "its result was discarded.")
                    : source)
                .Concat(changedSourcePaths.Select(sourcePath =>
                    new I18nIlUsageSourceScanResult(
                        Path.GetFileNameWithoutExtension(sourcePath),
                        sourcePath,
                        I18nUsageSourceScanStatus.Changed,
                        Array.Empty<I18nIlUsage>(),
                        new[]
                        {
                            $"'{sourcePath}' changed while the scan was running and was not analyzed.",
                        },
                        null,
                        false,
                        null)))
                .ToArray();
            return new I18nIlUsageScanResult(sources, GlobalWarnings, Performance);
        }
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

    /// <summary>
    /// Describes the measured Cecil stages for one candidate assembly.
    /// </summary>
    internal sealed class I18nCecilAssemblyScanPerformance
    {
        public I18nCecilAssemblyScanPerformance(
            string assemblyName,
            bool hasSymbols,
            double totalMilliseconds,
            double resolverMilliseconds,
            double readMilliseconds,
            double traversalMilliseconds,
            double locationResolutionMilliseconds)
        {
            AssemblyName = assemblyName;
            HasSymbols = hasSymbols;
            TotalMilliseconds = totalMilliseconds;
            ResolverMilliseconds = resolverMilliseconds;
            ReadMilliseconds = readMilliseconds;
            TraversalMilliseconds = traversalMilliseconds;
            LocationResolutionMilliseconds = locationResolutionMilliseconds;
        }

        public string AssemblyName { get; }

        public bool HasSymbols { get; }

        public double TotalMilliseconds { get; }

        public double ResolverMilliseconds { get; }

        public double ReadMilliseconds { get; }

        public double TraversalMilliseconds { get; }

        public double LocationResolutionMilliseconds { get; }
    }
}
