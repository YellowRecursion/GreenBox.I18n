using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageIntegerMatcherTests
{
    [Theory]
    [InlineData(1.4, "one: 1")]
    [InlineData(1.6, "other: 2")]
    public void Format_Integer_RoundsBeforePluralSelection(double value, string expected)
    {
        const string source =
            ".input {$count :integer}\n" +
            ".match $count\n" +
            "one {{one: {$count}}}\n" +
            "* {{other: {$count}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("count", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_Integer_SupportsOrdinalSelection()
    {
        const string source =
            ".input {$position :integer select=ordinal}\n" +
            ".match $position\n" +
            "one {{{$position}st}}\n" +
            "two {{{$position}nd}}\n" +
            "few {{{$position}rd}}\n" +
            "* {{{$position}th}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("position", 22.1m)).Text;

        Assert.Equal("22nd", result);
    }

    [Fact]
    public void Format_Integer_SupportsMaximumSignificantDigits()
    {
        const string source =
            ".input {$value :integer maximumSignificantDigits=2}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 123m)).Text;

        Assert.Equal("120", result);
    }

    [Theory]
    [InlineData("minimumFractionDigits=1")]
    [InlineData("maximumFractionDigits=1")]
    [InlineData("minimumSignificantDigits=2")]
    [InlineData("trailingZeroDisplay=stripIfInteger")]
    [InlineData("roundingIncrement=5")]
    public void Compile_IntegerRejectsUnsupportedOptions(string options)
    {
        string source = ".input {$value :integer " + options + "}\n{{{$value}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
    }
}
