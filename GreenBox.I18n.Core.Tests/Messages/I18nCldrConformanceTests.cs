using System.Text;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCldrConformanceTests
{
    [Fact]
    public void GeneratedRulesMatchOfficialCldrExamples()
    {
        var failures = new StringBuilder();
        int failureCount = 0;

        foreach (I18nCldrConformanceCases.Case testCase in I18nCldrConformanceCases.All)
        {
            string actual = I18nPluralData
                .SelectCldrSample(testCase.Locale, testCase.Ordinal, testCase.Sample)
                .ToString()
                .ToLowerInvariant();
            if (actual == testCase.Expected)
            {
                continue;
            }

            failureCount++;
            if (failureCount <= 20)
            {
                failures.AppendLine(
                    $"{testCase.Locale} {(testCase.Ordinal ? "ordinal" : "cardinal")} " +
                    $"{testCase.Sample}: expected {testCase.Expected}, got {actual}");
            }
        }

        Assert.True(
            failureCount == 0,
            $"{failureCount} of {I18nCldrConformanceCases.All.Length} CLDR examples failed.\n{failures}");
    }
}
