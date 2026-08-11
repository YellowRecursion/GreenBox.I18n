using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageMatcherTests
{
    [Theory]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1e0")]
    [InlineData("-0")]
    public void Compile_NonCanonicalExactKey_ReturnsDiagnostic(string key)
    {
        string source =
            ".input {$count :number}\n.match $count\n" +
            key + " {{Exact}}\n* {{Other}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }

    private const string EnglishItems =
        ".input {$count :number}\n" +
        ".match $count\n" +
        "one {{You have {$count} item.}}\n" +
        "*   {{You have {$count} items.}}";

    private const string RussianItems =
        ".input {$count :number}\n" +
        ".match $count\n" +
        "one {{У вас {$count} предмет.}}\n" +
        "few {{У вас {$count} предмета.}}\n" +
        "many {{У вас {$count} предметов.}}\n" +
        "* {{У вас {$count} предмета.}}";

    [Theory]
    [InlineData(1, "You have 1 item.")]
    [InlineData(2, "You have 2 items.")]
    [InlineData(21, "You have 21 items.")]
    public void Format_EnglishCardinal_SelectsPluralVariant(int count, string expected)
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile(EnglishItems).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", count));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData(1, "У вас 1 предмет.")]
    [InlineData(2, "У вас 2 предмета.")]
    [InlineData(5, "У вас 5 предметов.")]
    [InlineData(21, "У вас 21 предмет.")]
    [InlineData(22, "У вас 22 предмета.")]
    [InlineData(25, "У вас 25 предметов.")]
    public void Format_RussianCardinal_SelectsPluralVariant(int count, string expected)
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile(RussianItems).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("ru-RU"),
            ("count", count));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData(0, "No items.")]
    [InlineData(1, "One item.")]
    [InlineData(2, "2 items.")]
    public void Format_ExactNumberKey_TakesPriorityOverPluralCategory(int count, string expected)
    {
        const string source =
            ".input {$count :number}\n" +
            ".match $count\n" +
            "0 {{No items.}}\n" +
            "1 {{One item.}}\n" +
            "* {{{$count} items.}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", count));

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Compile_MatcherWithoutWildcard_ReturnsSyntaxDiagnostic()
    {
        const string source =
            ".input {$count :number}\n" +
            ".match $count\n" +
            "one {{One item.}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Null(compilation.Message);
        Assert.Equal(
            I18nMessageDiagnosticCodes.MissingFallbackVariant,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
