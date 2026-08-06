#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityCompilationAssembly = UnityEditor.Compilation.Assembly;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Finds self-identifying entry IDs embedded as 64-bit constants in Unity player assemblies.
    /// </summary>
    internal static class I18nIlUsageScanner
    {
        private const string MenuPath = "Tools/GreenBox I18n/Scan Entry Usage";
        private const int MaximumReportedLocationCount = 100;
        private const int MaximumReportedLocationsPerEntry = 10;

        [MenuItem(MenuPath, false, 100)]
        private static void ScanAndLog()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[GreenBox I18n] Wait for script compilation to finish before scanning entry usage.");
                return;
            }

            I18nIlUsageScanResult result = Scan();
            string report = FormatReport(result);
            if (result.Warnings.Count > 0)
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }
        }

        internal static string FormatReport(I18nIlUsageScanResult result)
        {
            var lines = new List<string>
            {
                $"[GreenBox I18n] IL usage scan completed in {result.ElapsedMilliseconds} ms. " +
                $"Scanned {result.ScannedAssemblyCount} player assembly(s) with Assets sources; " +
                $"found {result.Usages.Count} usage(s) across " +
                $"{result.Usages.Select(usage => usage.EntryId).Distinct().Count()} ID(s).",
            };

            int reportedLocationCount = 0;
            foreach (IGrouping<long, I18nIlUsage> group in result.Usages
                         .GroupBy(usage => usage.EntryId)
                         .OrderBy(group => group.Key))
            {
                if (reportedLocationCount >= MaximumReportedLocationCount)
                {
                    break;
                }

                I18nIlUsage[] orderedLocations = group
                    .OrderBy(usage => usage.AssetPath, StringComparer.Ordinal)
                    .ThenBy(usage => usage.Line)
                    .ToArray();
                int locationLimit = Math.Min(
                    MaximumReportedLocationsPerEntry,
                    MaximumReportedLocationCount - reportedLocationCount);
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
            var stopwatch = Stopwatch.StartNew();
            var usages = new HashSet<I18nIlUsage>();
            var warnings = new List<string>();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            int scannedAssemblyCount = 0;

            UnityCompilationAssembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (UnityCompilationAssembly assembly in assemblies)
            {
                if (!assembly.sourceFiles.Any(sourceFile => IsAssetsPath(sourceFile, projectRoot)))
                {
                    continue;
                }

                string assemblyPath = ResolvePath(assembly.outputPath, projectRoot);
                if (!File.Exists(assemblyPath))
                {
                    warnings.Add($"Skipped '{assembly.name}' because its compiled DLL was not found at '{assemblyPath}'.");
                    continue;
                }

                try
                {
                    ScanAssembly(assemblyPath, assembly.allReferences, projectRoot, usages);
                    scannedAssemblyCount++;
                }
                catch (Exception exception)
                {
                    warnings.Add($"Skipped '{assembly.name}': {exception.GetType().Name}: {exception.Message}");
                }
            }

            stopwatch.Stop();
            return new I18nIlUsageScanResult(
                usages
                    .OrderBy(usage => usage.EntryId)
                    .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                    .ThenBy(usage => usage.Line)
                    .ToArray(),
                warnings,
                scannedAssemblyCount,
                stopwatch.ElapsedMilliseconds);
        }

        private static void ScanAssembly(
            string assemblyPath,
            IReadOnlyList<string> referencePaths,
            string projectRoot,
            ISet<I18nIlUsage> usages)
        {
            bool hasSymbols = File.Exists(Path.ChangeExtension(assemblyPath, ".pdb"));
            using DefaultAssemblyResolver resolver = CreateAssemblyResolver(
                assemblyPath,
                referencePaths,
                projectRoot);
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                InMemory = true,
                ReadSymbols = hasSymbols,
                ReadingMode = ReadingMode.Immediate,
            };

            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in EnumerateTypes(module.Types))
                {
                    foreach (MethodDefinition method in type.Methods)
                    {
                        ScanMethod(method, projectRoot, usages);
                    }
                }
            }
        }

        private static DefaultAssemblyResolver CreateAssemblyResolver(
            string assemblyPath,
            IReadOnlyList<string> referencePaths,
            string projectRoot)
        {
            var resolver = new DefaultAssemblyResolver();
            var searchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSearchDirectory(Path.GetDirectoryName(assemblyPath));

            foreach (string referencePath in referencePaths)
            {
                string resolvedReferencePath = ResolvePath(referencePath, projectRoot);
                AddSearchDirectory(Path.GetDirectoryName(resolvedReferencePath));
            }

            return resolver;

            void AddSearchDirectory(string? directory)
            {
                if (directory == null || directory.Length == 0 || !Directory.Exists(directory))
                {
                    return;
                }

                string searchDirectory = directory;
                if (searchDirectories.Add(searchDirectory))
                {
                    resolver.AddSearchDirectory(searchDirectory);
                }
            }
        }

        private static void ScanMethod(
            MethodDefinition method,
            string projectRoot,
            ISet<I18nIlUsage> usages)
        {
            if (!method.HasBody)
            {
                return;
            }

            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.OpCode.Code != Code.Ldc_I8 ||
                    instruction.Operand is not long entryId ||
                    !I18nEntryId.IsValid(entryId))
                {
                    continue;
                }

                SequencePoint? sequencePoint = FindSequencePoint(method, instruction.Offset, projectRoot);
                if (sequencePoint == null)
                {
                    continue;
                }

                if (TryGetAssetPath(sequencePoint.Document.Url, projectRoot, out string assetPath))
                {
                    usages.Add(new I18nIlUsage(entryId, assetPath, sequencePoint.StartLine));
                }
            }
        }

        private static SequencePoint? FindSequencePoint(
            MethodDefinition method,
            int instructionOffset,
            string projectRoot)
        {
            SequencePoint? nearest = null;
            foreach (SequencePoint sequencePoint in method.DebugInformation.SequencePoints)
            {
                if (sequencePoint.Offset > instructionOffset)
                {
                    break;
                }

                if (!sequencePoint.IsHidden &&
                    TryGetAssetPath(sequencePoint.Document.Url, projectRoot, out _))
                {
                    nearest = sequencePoint;
                }
            }

            return nearest;
        }

        private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (TypeDefinition type in roots)
            {
                yield return type;
                foreach (TypeDefinition nestedType in EnumerateTypes(type.NestedTypes))
                {
                    yield return nestedType;
                }
            }
        }

        private static bool IsAssetsPath(string path, string projectRoot)
        {
            return TryGetAssetPath(path, projectRoot, out _);
        }

        private static bool TryGetAssetPath(
            string path,
            string projectRoot,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string localPath = path;
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.IsFile)
            {
                localPath = uri.LocalPath;
            }

            string fullPath = ResolvePath(localPath, projectRoot).Replace('\\', '/');
            string assetsRoot = Path.Combine(projectRoot, "Assets").Replace('\\', '/').TrimEnd('/');
            string assetsPrefix = assetsRoot + "/";
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetPath = "Assets/" + fullPath.Substring(assetsPrefix.Length);
            return true;
        }

        private static string ResolvePath(string path, string projectRoot)
        {
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
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
            long elapsedMilliseconds)
        {
            Usages = usages;
            Warnings = warnings;
            ScannedAssemblyCount = scannedAssemblyCount;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public IReadOnlyList<I18nIlUsage> Usages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int ScannedAssemblyCount { get; }

        public long ElapsedMilliseconds { get; }
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
