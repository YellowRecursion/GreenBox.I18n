#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using GreenBox.I18n.Unity.Editor.Usage;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Development.Editor
{
    /// <summary>
    /// Runs the IL usage scanner against deterministic player-code fixtures.
    /// </summary>
    internal static class I18nIlUsageScannerSelfTest
    {
        private const string MenuPath = "Tools/GreenBox I18n/Test Entry Usage Scanner";
        private const string FixturePath = "Assets/I18nDevelopment/Test/I18nUsageScanTestFixture.cs";

        private static readonly ExpectedUsage[] ExpectedUsages =
        {
            new ExpectedUsage("direct literal", 3857333080842830204),
            new ExpectedUsage("const field", 3857333080842830453),
            new ExpectedUsage("local variable", 3857333080842830706),
            new ExpectedUsage("same-line deduplication", 3857333080842830951),
            new ExpectedUsage("static readonly initializer", 3857333080842831200),
            new ExpectedUsage("nested type", 3857333080842831465),
        };

        private static readonly ExpectedUsage[] ExpectedAbsentUsages =
        {
            new ExpectedUsage("comment", 3857333080842831726),
            new ExpectedUsage("disabled preprocessor branch", 3857333080842831939),
        };

        [MenuItem(MenuPath, false, 101)]
        private static void Run()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[GreenBox I18n] Wait for script compilation to finish before testing the usage scanner.");
                return;
            }

            I18nIlUsageScanResult result = I18nIlUsageScanner.Scan();
            var errors = new List<string>();
            if (result.Warnings.Count > 0)
            {
                errors.AddRange(result.Warnings.Select(warning => $"scan warning: {warning}"));
            }

            I18nIlUsage[] fixtureUsages = result.Usages
                .Where(usage => string.Equals(usage.AssetPath, FixturePath, StringComparison.Ordinal))
                .ToArray();

            foreach (ExpectedUsage expectation in ExpectedUsages)
            {
                int actualCount = fixtureUsages.Count(usage => usage.EntryId == expectation.EntryId);
                if (actualCount != 1)
                {
                    errors.Add($"{expectation.Name}: expected 1 location, found {actualCount}");
                }
            }

            foreach (ExpectedUsage expectation in ExpectedAbsentUsages)
            {
                int actualCount = fixtureUsages.Count(usage => usage.EntryId == expectation.EntryId);
                if (actualCount != 0)
                {
                    errors.Add($"{expectation.Name}: expected no IL, found {actualCount} location(s)");
                }
            }

            var knownIds = new HashSet<long>(ExpectedUsages
                .Concat(ExpectedAbsentUsages)
                .Select(expectation => expectation.EntryId));
            foreach (I18nIlUsage unexpected in fixtureUsages.Where(usage => !knownIds.Contains(usage.EntryId)))
            {
                errors.Add($"unexpected ID {unexpected.EntryId} at {unexpected.AssetPath}:{unexpected.Line}");
            }

            if (errors.Count == 0)
            {
                Debug.Log(
                    $"[GreenBox I18n] PASS - entry usage scanner self-test completed in " +
                    $"{result.ElapsedMilliseconds} ms. All {ExpectedUsages.Length + ExpectedAbsentUsages.Length} cases passed.");
                return;
            }

            Debug.LogError(
                $"[GreenBox I18n] FAIL - entry usage scanner self-test found {errors.Count} problem(s):" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => $"  - {error}")));
        }

        private sealed class ExpectedUsage
        {
            public ExpectedUsage(string name, long entryId)
            {
                Name = name;
                EntryId = entryId;
            }

            public string Name { get; }

            public long EntryId { get; }
        }
    }
}
