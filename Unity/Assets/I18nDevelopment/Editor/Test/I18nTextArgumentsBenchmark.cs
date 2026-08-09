#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using Unity.Profiling;
using UnityEditor;

namespace GreenBox.I18n.Development.Editor
{
    /// <summary>
    /// Compares candidate public APIs for passing named message arguments.
    /// This development-only benchmark intentionally measures argument transport
    /// and lookup without involving a message parser or formatter.
    /// </summary>
    internal static class I18nTextArgumentsBenchmark
    {
        private const string MenuPath = "Tools/GreenBox I18n/Benchmark Text Arguments";
        private const int WarmupIterations = 10_000;
        private const int MeasuredIterations = 500_000;
        private const int AllocationIterations = 100_000;
        private const int SampleCount = 3;

        private static int _checksum;

        [MenuItem(MenuPath, false, 103)]
        private static void Run()
        {
            var cases = new[]
            {
                new BenchmarkCase("Baseline", ConsumeNoArguments),
                new BenchmarkCase(
                    "Anonymous object, 1 argument",
                    () => ConsumeAnonymous(new { count = 5 })),
                new BenchmarkCase(
                    "Generic tuple, 1 argument",
                    () => ConsumeTuple(("count", 5))),
                new BenchmarkCase(
                    "params tuple, 1 argument",
                    () => ConsumeParams(("count", 5))),
                new BenchmarkCase(
                    "Anonymous object, 2 arguments",
                    () => ConsumeAnonymous(new { count = 5, gender = TestGender.Female })),
                new BenchmarkCase(
                    "Generic tuples, 2 arguments",
                    () => ConsumeTuples(("count", 5), ("gender", TestGender.Female))),
                new BenchmarkCase(
                    "params tuples, 2 arguments",
                    () => ConsumeParams(("count", 5), ("gender", TestGender.Female))),
                new BenchmarkCase(
                    "Typed pipeline, 1 tuple argument",
                    () => ConsumeTypedSource(
                        new TupleArgumentSource<int>(("count", 5)),
                        "count")),
                new BenchmarkCase(
                    "Typed pipeline, 2 tuple arguments",
                    () => ConsumeTypedSource(
                        new TupleArgumentSource<int, TestGender>(
                            ("count", 5),
                            ("gender", TestGender.Female)),
                        "count",
                        "gender")),
            };

            foreach (BenchmarkCase benchmarkCase in cases)
            {
                Execute(benchmarkCase.Action, WarmupIterations);
            }

            BenchmarkResult[] results = cases
                .Select(Measure)
                .ToArray();

            string report = string.Join(
                Environment.NewLine,
                results.Select(result =>
                    $"  {result.Name}: {result.NanosecondsPerOperation:F1} ns/op, " +
                    $"{FormatAllocations(result)}"));

            I18nLog.Info(
                "Text argument API benchmark (Unity Editor, transport + named lookup):" +
                Environment.NewLine +
                $"  {MeasuredIterations:N0} operations per sample; median of {SampleCount} samples." +
                Environment.NewLine +
                $"  Allocations measured separately over {AllocationIterations:N0} operations " +
                "with Unity Profiler counters." +
                Environment.NewLine +
                report +
                Environment.NewLine +
                $"  Checksum: {_checksum}" +
                Environment.NewLine +
                "Run this in a non-development build later to validate IL2CPP separately.");
        }

        private static BenchmarkResult Measure(BenchmarkCase benchmarkCase)
        {
            var elapsedTicks = new long[SampleCount];

            for (int sample = 0; sample < SampleCount; sample++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long timestampBefore = Stopwatch.GetTimestamp();

                Execute(benchmarkCase.Action, MeasuredIterations);

                elapsedTicks[sample] = Stopwatch.GetTimestamp() - timestampBefore;
            }

            Array.Sort(elapsedTicks);
            long medianTicks = elapsedTicks[SampleCount / 2];
            AllocationResult allocations = MeasureAllocations(benchmarkCase.Action);

            return new BenchmarkResult(
                benchmarkCase.Name,
                medianTicks * 1_000_000_000d / Stopwatch.Frequency / MeasuredIterations,
                allocations.CountPerOperation);
        }

        private static AllocationResult MeasureAllocations(Action action)
        {
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;

            using ProfilerRecorder eventRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "GC.Alloc",
                1,
                options);

            Execute(action, AllocationIterations);

            eventRecorder.Stop();

            double? countPerOperation = eventRecorder.Valid
                ? GetAllocationEventCount(eventRecorder) / (double)AllocationIterations
                : null;

            return new AllocationResult(countPerOperation);
        }

        private static long GetAllocationEventCount(ProfilerRecorder recorder)
        {
            return recorder.Count == 0
                ? 0
                : recorder.GetSample(0).Count;
        }

        private static string FormatAllocations(BenchmarkResult result)
        {
            string count = result.AllocationsPerOperation.HasValue
                ? $"{result.AllocationsPerOperation.Value:F2} alloc/op"
                : "alloc/op unavailable";
            return count;
        }

        private static void Execute(Action action, int iterations)
        {
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                action();
            }
        }

        private static void ConsumeNoArguments()
        {
            _checksum = unchecked((_checksum * 397) ^ 17);
        }

        private static void ConsumeAnonymous<TArguments>(TArguments arguments)
        {
            int hash = 17;
            foreach (PropertyInfo property in AnonymousProperties<TArguments>.Items)
            {
                object? value = property.GetValue(arguments);
                hash = unchecked((hash * 397) ^ property.Name.GetHashCode());
                hash = unchecked((hash * 397) ^ (value?.GetHashCode() ?? 0));
            }

            _checksum = unchecked((_checksum * 397) ^ hash);
        }

        private static void ConsumeTuple<T>((string Name, T Value) argument)
        {
            _checksum = unchecked(
                (_checksum * 397) ^
                ArgumentHash(argument.Name, argument.Value));
        }

        private static void ConsumeTuples<T1, T2>(
            (string Name, T1 Value) first,
            (string Name, T2 Value) second)
        {
            int hash = ArgumentHash(first.Name, first.Value);
            hash = unchecked((hash * 397) ^ ArgumentHash(second.Name, second.Value));
            _checksum = unchecked((_checksum * 397) ^ hash);
        }

        private static void ConsumeParams(params (string Name, object? Value)[] arguments)
        {
            int hash = 17;
            foreach ((string name, object? value) in arguments)
            {
                hash = unchecked((hash * 397) ^ name.GetHashCode());
                hash = unchecked((hash * 397) ^ (value?.GetHashCode() ?? 0));
            }

            _checksum = unchecked((_checksum * 397) ^ hash);
        }

        private static int ArgumentHash<T>(string name, T value)
        {
            int valueHash = value is null
                ? 0
                : EqualityComparer<T>.Default.GetHashCode(value);
            return unchecked((name.GetHashCode() * 397) ^ valueHash);
        }

        private static void ConsumeTypedSource<TSource>(TSource source, string argumentName)
            where TSource : struct, IArgumentSource
        {
            var visitor = new HashArgumentVisitor();
            if (!source.TryVisit(argumentName, ref visitor))
            {
                throw new InvalidOperationException($"Argument '{argumentName}' was not found.");
            }

            _checksum = unchecked((_checksum * 397) ^ visitor.Hash);
        }

        private static void ConsumeTypedSource<TSource>(
            TSource source,
            string firstArgumentName,
            string secondArgumentName)
            where TSource : struct, IArgumentSource
        {
            var visitor = new HashArgumentVisitor();
            if (!source.TryVisit(firstArgumentName, ref visitor))
            {
                throw new InvalidOperationException(
                    $"Argument '{firstArgumentName}' was not found.");
            }

            if (!source.TryVisit(secondArgumentName, ref visitor))
            {
                throw new InvalidOperationException(
                    $"Argument '{secondArgumentName}' was not found.");
            }

            _checksum = unchecked((_checksum * 397) ^ visitor.Hash);
        }

        private static class AnonymousProperties<TArguments>
        {
            public static readonly PropertyInfo[] Items = typeof(TArguments).GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
        }

        private interface IArgumentVisitor
        {
            void Visit<T>(T value);
        }

        private interface IArgumentSource
        {
            bool TryVisit<TVisitor>(string name, ref TVisitor visitor)
                where TVisitor : struct, IArgumentVisitor;
        }

        private struct HashArgumentVisitor : IArgumentVisitor
        {
            public int Hash { get; private set; }

            public void Visit<T>(T value)
            {
                int valueHash = value is null
                    ? 0
                    : EqualityComparer<T>.Default.GetHashCode(value);
                Hash = unchecked((Hash * 397) ^ valueHash);
            }
        }

        private readonly struct TupleArgumentSource<T> : IArgumentSource
        {
            private readonly (string Name, T Value) _argument;

            public TupleArgumentSource((string Name, T Value) argument)
            {
                _argument = argument;
            }

            public bool TryVisit<TVisitor>(string name, ref TVisitor visitor)
                where TVisitor : struct, IArgumentVisitor
            {
                if (!string.Equals(name, _argument.Name, StringComparison.Ordinal))
                {
                    return false;
                }

                visitor.Visit(_argument.Value);
                return true;
            }
        }

        private readonly struct TupleArgumentSource<T1, T2> : IArgumentSource
        {
            private readonly (string Name, T1 Value) _first;
            private readonly (string Name, T2 Value) _second;

            public TupleArgumentSource(
                (string Name, T1 Value) first,
                (string Name, T2 Value) second)
            {
                _first = first;
                _second = second;
            }

            public bool TryVisit<TVisitor>(string name, ref TVisitor visitor)
                where TVisitor : struct, IArgumentVisitor
            {
                if (string.Equals(name, _first.Name, StringComparison.Ordinal))
                {
                    visitor.Visit(_first.Value);
                    return true;
                }

                if (!string.Equals(name, _second.Name, StringComparison.Ordinal))
                {
                    return false;
                }

                visitor.Visit(_second.Value);
                return true;
            }
        }

        private sealed class BenchmarkCase
        {
            public BenchmarkCase(string name, Action action)
            {
                Name = name;
                Action = action;
            }

            public string Name { get; }

            public Action Action { get; }
        }

        private readonly struct BenchmarkResult
        {
            public BenchmarkResult(
                string name,
                double nanosecondsPerOperation,
                double? allocationsPerOperation)
            {
                Name = name;
                NanosecondsPerOperation = nanosecondsPerOperation;
                AllocationsPerOperation = allocationsPerOperation;
            }

            public string Name { get; }

            public double NanosecondsPerOperation { get; }

            public double? AllocationsPerOperation { get; }
        }

        private readonly struct AllocationResult
        {
            public AllocationResult(double? countPerOperation)
            {
                CountPerOperation = countPerOperation;
            }

            public double? CountPerOperation { get; }
        }

        private enum TestGender
        {
            Female,
        }
    }
}
