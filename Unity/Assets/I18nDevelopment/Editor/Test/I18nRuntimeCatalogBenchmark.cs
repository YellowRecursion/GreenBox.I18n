#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using Unity.Profiling;
using UnityEditor;

namespace GreenBox.I18n.Development.Editor
{
    /// <summary>
    /// Exercises the complete compiled-catalog pipeline with a realistically large catalog.
    /// The benchmark is development-only and never reads or modifies the project catalog.
    /// </summary>
    internal static class I18nRuntimeCatalogBenchmark
    {
        private const string MenuPath =
            "Tools/GreenBox I18n/Benchmark Runtime Catalog (10,000 Entries)";
        private const int EntryCount = 10_000;
        private const int LookupIterations = 500_000;
        private const int FormatIterations = 100_000;

        private static int _checksum;

        [MenuItem(MenuPath, false, 104)]
        private static void Run()
        {
            try
            {
                EditorUtility.DisplayProgressBar(
                    "GreenBox I18n",
                    "Building an in-memory catalog with 10,000 entries...",
                    0.1f);

                BuildResult build = BuildBinary();

                EditorUtility.DisplayProgressBar(
                    "GreenBox I18n",
                    "Loading the compiled runtime catalog...",
                    0.65f);

                ForceCollection();
                long memoryBeforeDeserialize = GC.GetTotalMemory(false);
                long deserializeStart = Stopwatch.GetTimestamp();
                I18nCompiledCatalog compiled = I18nCompiledCatalogBinary.Deserialize(build.Binary);
                double deserializeMilliseconds = ElapsedMilliseconds(deserializeStart);
                ForceCollection();
                long compiledCatalogBytes = Math.Max(
                    0,
                    GC.GetTotalMemory(false) - memoryBeforeDeserialize);

                long memoryBeforeRuntime = GC.GetTotalMemory(false);
                long runtimeStart = Stopwatch.GetTimestamp();
                var runtime = new I18nRuntime(compiled);
                double runtimeMilliseconds = ElapsedMilliseconds(runtimeStart);
                ForceCollection();
                long runtimeBytes = Math.Max(0, GC.GetTotalMemory(false) - memoryBeforeRuntime);

                EditorUtility.DisplayProgressBar(
                    "GreenBox I18n",
                    "Verifying lookups and message formatting...",
                    0.85f);

                Verify(runtime, build);
                OperationResult lookup = MeasureOperations(
                    () => Consume(runtime.Text(build.LastId)),
                    LookupIterations);
                OperationResult format = MeasureOperations(
                    () => Consume(runtime.Text(build.MessageId, ("count", 42))),
                    FormatIterations);

                I18nLog.Info(
                    "Runtime catalog benchmark PASSED:" + Environment.NewLine +
                    $"  Catalog: {EntryCount:N0} entries, 2 populated locales." + Environment.NewLine +
                    $"  Build source model: {build.BuildSourceMilliseconds:F1} ms." + Environment.NewLine +
                    $"  Compile messages + flat catalog: {build.CompileMilliseconds:F1} ms." + Environment.NewLine +
                    $"  Serialize binary: {build.SerializeMilliseconds:F1} ms." + Environment.NewLine +
                    $"  Binary size: {FormatBytes(build.Binary.LongLength)}." + Environment.NewLine +
                    $"  Deserialize binary: {deserializeMilliseconds:F1} ms, " +
                    $"retained managed memory ~{FormatBytes(compiledCatalogBytes)}." + Environment.NewLine +
                    $"  Construct runtime: {runtimeMilliseconds:F1} ms, " +
                    $"retained managed memory ~{FormatBytes(runtimeBytes)}." + Environment.NewLine +
                    $"  Plain lookup: {lookup.NanosecondsPerOperation:F1} ns/op, " +
                    $"{FormatAllocations(lookup)}." + Environment.NewLine +
                    $"  MF2 formatting: {format.NanosecondsPerOperation:F1} ns/op, " +
                    $"{FormatAllocations(format)}." + Environment.NewLine +
                    $"  Checksum: {_checksum}." + Environment.NewLine +
                    "Memory values are retained-heap observations after forced GC, not exact object sizes.");

                GC.KeepAlive(runtime);
                GC.KeepAlive(compiled);
                GC.KeepAlive(build.Binary);
            }
            catch (Exception exception)
            {
                I18nLog.Error(
                    "Runtime catalog benchmark FAILED:" + Environment.NewLine + exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static BuildResult BuildBinary()
        {
            long sourceStart = Stopwatch.GetTimestamp();
            I18nCatalog source = CreateSourceCatalog(
                out long messageId,
                out long middleId,
                out long lastId);
            double sourceMilliseconds = ElapsedMilliseconds(sourceStart);

            long compileStart = Stopwatch.GetTimestamp();
            I18nCompiledCatalogCompilation compilation =
                I18nCompiledCatalogCompiler.Compile(source);
            double compileMilliseconds = ElapsedMilliseconds(compileStart);
            if (!compilation.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Generated catalog produced {compilation.Diagnostics.Count} diagnostic(s).");
            }

            long serializeStart = Stopwatch.GetTimestamp();
            byte[] binary = I18nCompiledCatalogBinary.Serialize(compilation.Catalog!);
            double serializeMilliseconds = ElapsedMilliseconds(serializeStart);

            return new BuildResult(
                binary,
                messageId,
                middleId,
                lastId,
                sourceMilliseconds,
                compileMilliseconds,
                serializeMilliseconds);
        }

        private static I18nCatalog CreateSourceCatalog(
            out long messageId,
            out long middleId,
            out long lastId)
        {
            var catalog = new I18nCatalog
            {
                DefaultLocale = "en",
                Locales =
                {
                    new I18nLocaleDefinition
                    {
                        Id = "en",
                        DisplayName = "English",
                        Culture = "en-US",
                    },
                    new I18nLocaleDefinition
                    {
                        Id = "ru",
                        DisplayName = "Русский",
                        Culture = "ru-RU",
                        Fallback = "en",
                    },
                },
            };

            messageId = 0;
            middleId = 0;
            lastId = 0;
            for (int index = 0; index < EntryCount; index++)
            {
                long id = I18nEntryId.Generate();
                string en = "Value " + index.ToString(CultureInfo.InvariantCulture);
                string ru = "Значение " + index.ToString(CultureInfo.InvariantCulture);
                if (index == 0)
                {
                    messageId = id;
                    en = ".input {$count :number}\n{{Items: {$count}}}";
                    ru = ".input {$count :number}\n{{Предметы: {$count}}}";
                }

                if (index == EntryCount / 2)
                {
                    middleId = id;
                }

                if (index == EntryCount - 1)
                {
                    lastId = id;
                }

                catalog.Entries.Add(new I18nEntry
                {
                    Id = id.ToString(CultureInfo.InvariantCulture),
                    Path = "Benchmark.Entry" + index.ToString(CultureInfo.InvariantCulture),
                    Locales =
                    {
                        ["en"] = new I18nLocaleValue { Text = en },
                        ["ru"] = new I18nLocaleValue { Text = ru },
                    },
                });
            }

            return catalog;
        }

        private static void Verify(I18nRuntime runtime, BuildResult build)
        {
            AssertEqual("Value 5000", runtime.Text(build.MiddleId));
            AssertEqual("Value 9999", runtime.Text(build.LastId));
            AssertEqual("Items: 42", runtime.Text(build.MessageId, ("count", 42)));

            runtime.SetLocale("ru");
            AssertEqual("Значение 5000", runtime.Text(build.MiddleId));
            AssertEqual("Предметы: 42", runtime.Text(build.MessageId, ("count", 42)));
            runtime.SetLocale("en");
        }

        private static OperationResult MeasureOperations(Action action, int iterations)
        {
            const int warmupIterations = 10_000;
            Execute(action, warmupIterations);

            ForceCollection();
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using ProfilerRecorder allocationRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "GC.Alloc",
                1,
                options);

            long start = Stopwatch.GetTimestamp();
            Execute(action, iterations);
            long elapsed = Stopwatch.GetTimestamp() - start;
            allocationRecorder.Stop();

            double? allocationsPerOperation = allocationRecorder.Valid
                ? GetAllocationEventCount(allocationRecorder) / (double)iterations
                : null;
            return new OperationResult(
                elapsed * 1_000_000_000d / Stopwatch.Frequency / iterations,
                allocationsPerOperation);
        }

        private static void Execute(Action action, int iterations)
        {
            for (int index = 0; index < iterations; index++)
            {
                action();
            }
        }

        private static long GetAllocationEventCount(ProfilerRecorder recorder)
        {
            return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }

        private static void Consume(string value)
        {
            _checksum = unchecked((_checksum * 397) ^ value.Length);
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected '{expected}', but received '{actual}'.");
            }
        }

        private static double ElapsedMilliseconds(long startedAt)
        {
            return (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;
        }

        private static void ForceCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static string FormatAllocations(OperationResult result)
        {
            return result.AllocationsPerOperation.HasValue
                ? $"{result.AllocationsPerOperation.Value:F2} alloc/op"
                : "alloc/op unavailable";
        }

        private static string FormatBytes(long bytes)
        {
            const double kibibyte = 1024d;
            const double mebibyte = kibibyte * 1024d;
            return bytes >= mebibyte
                ? $"{bytes / mebibyte:F1} MiB"
                : $"{bytes / kibibyte:F1} KiB";
        }

        private sealed class BuildResult
        {
            public BuildResult(
                byte[] binary,
                long messageId,
                long middleId,
                long lastId,
                double buildSourceMilliseconds,
                double compileMilliseconds,
                double serializeMilliseconds)
            {
                Binary = binary;
                MessageId = messageId;
                MiddleId = middleId;
                LastId = lastId;
                BuildSourceMilliseconds = buildSourceMilliseconds;
                CompileMilliseconds = compileMilliseconds;
                SerializeMilliseconds = serializeMilliseconds;
            }

            public byte[] Binary { get; }
            public long MessageId { get; }
            public long MiddleId { get; }
            public long LastId { get; }
            public double BuildSourceMilliseconds { get; }
            public double CompileMilliseconds { get; }
            public double SerializeMilliseconds { get; }
        }

        private readonly struct OperationResult
        {
            public OperationResult(double nanosecondsPerOperation, double? allocationsPerOperation)
            {
                NanosecondsPerOperation = nanosecondsPerOperation;
                AllocationsPerOperation = allocationsPerOperation;
            }

            public double NanosecondsPerOperation { get; }
            public double? AllocationsPerOperation { get; }
        }
    }
}
