#nullable enable

using System;
using System.Collections.Generic;

namespace GreenBox.I18n.Usage.Analysis
{
    internal sealed class I18nIlUsageScanResult
    {
        public I18nIlUsageScanResult(
            IReadOnlyList<I18nIlUsage> usages,
            IReadOnlyList<string> warnings,
            int scannedAssemblyCount,
            int candidateAssemblyCount,
            IReadOnlyList<I18nCecilAssemblyScanPerformance> cecilPerformance,
            I18nUsageScanPerformance performance,
            IReadOnlyList<string> changedSourcePaths)
        {
            Usages = new List<I18nIlUsage>(usages).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            ScannedAssemblyCount = scannedAssemblyCount;
            CandidateAssemblyCount = candidateAssemblyCount;
            CecilPerformance = new List<I18nCecilAssemblyScanPerformance>(cecilPerformance).AsReadOnly();
            Performance = performance;
            ChangedSourcePaths = new List<string>(changedSourcePaths).AsReadOnly();
        }

        public IReadOnlyList<I18nIlUsage> Usages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int ScannedAssemblyCount { get; }

        public int CandidateAssemblyCount { get; }

        public IReadOnlyList<I18nCecilAssemblyScanPerformance> CecilPerformance { get; }

        public I18nUsageScanPerformance Performance { get; }

        public IReadOnlyList<string> ChangedSourcePaths { get; }

        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;

        public bool IsStable => ChangedSourcePaths.Count == 0;
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
