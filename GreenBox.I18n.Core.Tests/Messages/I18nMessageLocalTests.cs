using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageLocalTests
{
    [Fact]
    public void Format_Local_ReusesFormattedInput()
    {
        const string source =
            ".input {$value :number maximumFractionDigits=2}\n" +
            ".local $rounded = {$value :integer}\n" +
            "{{Raw: {$value}; rounded: {$rounded}}}";
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        string result = compilation.Message!.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1.6m)).Text;

        Assert.Equal(new[] { "value" }, compilation.Message.ArgumentNames);
        Assert.Equal("Raw: 1.6; rounded: 2", result);
    }

    [Fact]
    public void Format_ChainedLocals_AreFlattenedToExternalArgument()
    {
        const string source =
            ".input {$value}\n" +
            ".local $number={$value :number maximumFractionDigits=2}\n" +
            ".local $short = {$number :number maximumFractionDigits=1}\n" +
            "{{{$short}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1.26m)).Text;

        Assert.Equal("1.3", result);
    }

    [Theory]
    [InlineData(0.014, "one")]
    [InlineData(0.015, "other")]
    public void Format_Local_CanBeUsedAsMatcherSelector(double value, string expected)
    {
        const string source =
            ".input {$ratio :number}\n" +
            ".local $percent = {$ratio :percent}\n" +
            ".match $percent\n" +
            "one {{one}}\n" +
            "* {{other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("ratio", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(".local $copy = {$missing}")]
    [InlineData(".local $value = {$value}")]
    [InlineData(".local $later = {$next}\n.local $next = {$value}")]
    public void Compile_InvalidLocal_ReturnsDiagnostic(string local)
    {
        string source = ".input {$value}\n" + local + "\n{{{$value}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
