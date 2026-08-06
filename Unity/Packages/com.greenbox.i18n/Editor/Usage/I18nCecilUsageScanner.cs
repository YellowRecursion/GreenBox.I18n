#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Performs authoritative entry usage analysis for one candidate assembly using Mono.Cecil.
    /// </summary>
    internal static class I18nCecilUsageScanner
    {
        internal static I18nCecilAssemblyScanPerformance Scan(
            string assemblyName,
            string assemblyPath,
            IReadOnlyList<string> referencePaths,
            string projectRoot,
            ISet<I18nIlUsage> usages,
            I18nUsageScanProfiler profiler)
        {
            var totalStopwatch = Stopwatch.StartNew();
            bool hasSymbols = File.Exists(Path.ChangeExtension(assemblyPath, ".pdb"));

            var resolverStopwatch = Stopwatch.StartNew();
            using DefaultAssemblyResolver resolver = CreateAssemblyResolver(
                assemblyPath,
                referencePaths,
                projectRoot);
            resolverStopwatch.Stop();

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                InMemory = false,
                ReadSymbols = hasSymbols,
                ReadingMode = ReadingMode.Deferred,
            };

            var readStopwatch = Stopwatch.StartNew();
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
            readStopwatch.Stop();
            profiler.Sample();

            long locationResolutionTicks = 0;
            var traversalStopwatch = Stopwatch.StartNew();
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in EnumerateTypes(module.Types))
                {
                    foreach (MethodDefinition method in type.Methods)
                    {
                        ScanMethod(method, projectRoot, usages, ref locationResolutionTicks);
                    }
                }
            }

            traversalStopwatch.Stop();
            totalStopwatch.Stop();

            long traversalTicks = Math.Max(
                0,
                traversalStopwatch.ElapsedTicks - locationResolutionTicks);
            return new I18nCecilAssemblyScanPerformance(
                assemblyName,
                hasSymbols,
                ToMilliseconds(totalStopwatch.ElapsedTicks),
                ToMilliseconds(resolverStopwatch.ElapsedTicks),
                ToMilliseconds(readStopwatch.ElapsedTicks),
                ToMilliseconds(traversalTicks),
                ToMilliseconds(locationResolutionTicks));
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
                string resolvedReferencePath = I18nUsagePath.Resolve(referencePath, projectRoot);
                AddSearchDirectory(Path.GetDirectoryName(resolvedReferencePath));
            }

            return resolver;

            void AddSearchDirectory(string? directory)
            {
                if (directory == null || directory.Length == 0 || !Directory.Exists(directory))
                {
                    return;
                }

                if (searchDirectories.Add(directory))
                {
                    resolver.AddSearchDirectory(directory);
                }
            }
        }

        private static void ScanMethod(
            MethodDefinition method,
            string projectRoot,
            ISet<I18nIlUsage> usages,
            ref long locationResolutionTicks)
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

                long locationResolutionStart = Stopwatch.GetTimestamp();
                try
                {
                    SequencePoint? sequencePoint = FindSequencePoint(method, instruction.Offset, projectRoot);
                    if (sequencePoint == null)
                    {
                        continue;
                    }

                    if (I18nUsagePath.TryGetAssetPath(
                            sequencePoint.Document.Url,
                            projectRoot,
                            out string assetPath))
                    {
                        usages.Add(new I18nIlUsage(entryId, assetPath, sequencePoint.StartLine));
                    }
                }
                finally
                {
                    locationResolutionTicks += Stopwatch.GetTimestamp() - locationResolutionStart;
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
                    I18nUsagePath.TryGetAssetPath(sequencePoint.Document.Url, projectRoot, out _))
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

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
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
