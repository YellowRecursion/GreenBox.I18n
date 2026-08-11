using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageOffsetTests
{
    [Fact]
    public void Format_SubtractOffset_FormatsAdjustedValue()
    {
        const string source =
            ".input {$count :integer}\n" +
            ".local $others = {$count :offset subtract=1}\n" +
            "{{You and {$others} others}}";
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        string result = compilation.Message!.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", 5)).Text;

        Assert.Equal(new[] { "count" }, compilation.Message.ArgumentNames);
        Assert.Equal("You and 4 others", result);
    }

    [Theory]
    [InlineData(2, "one other")]
    [InlineData(3, "2 others")]
    public void Format_OffsetValue_IsUsedForPluralSelection(int count, string expected)
    {
        const string source =
            ".input {$count :integer}\n" +
            ".local $others = {$count :offset subtract=1}\n" +
            ".match $others\n" +
            "one {{one other}}\n" +
            "* {{{$others} others}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", count)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_ChainedOffsets_AreCombinedAtCompileTime()
    {
        const string source =
            ".input {$count :integer}\n" +
            ".local $plus = {$count :offset add=3}\n" +
            ".local $result = {$plus :offset subtract=1}\n" +
            "{{{$result}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", 5)).Text;

        Assert.Equal("7", result);
    }

    [Fact]
    public void Format_OffsetOverflow_ReturnsReadableFallbackAndDiagnostic()
    {
        const string source =
            ".input {$count :integer}\n" +
            ".local $adjusted = {$count :offset add=1}\n" +
            "{{{$adjusted}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("count", decimal.MaxValue));

        Assert.Equal("{$count}", result.Text);
        Assert.False(result.IsSuccess);
        I18nMessageDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nMessageDiagnosticCodes.UnsupportedOperation, diagnostic.Code);
        Assert.Equal("count", diagnostic.ArgumentName);
    }

    [Theory]
    [InlineData(":offset")]
    [InlineData(":offset add=1 subtract=1")]
    [InlineData(":offset value=1")]
    [InlineData(":offset subtract=-1")]
    [InlineData(":offset subtract=100")]
    public void Compile_InvalidOffset_ReturnsDiagnostic(string annotation)
    {
        string source =
            ".input {$count :integer}\n" +
            ".local $adjusted = {$count " + annotation + "}\n" +
            "{{{$adjusted}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
