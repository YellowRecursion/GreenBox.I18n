using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageOrdinalMatcherTests
{
    private const string EnglishPosition =
        ".input {$position :number select=ordinal}\n" +
        ".match $position\n" +
        "one {{{$position}st}}\n" +
        "two {{{$position}nd}}\n" +
        "few {{{$position}rd}}\n" +
        "* {{{$position}th}}";

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    public void Format_OrdinalNumber_UsesCldrOrdinalCategory(int position, string expected)
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(EnglishPosition);
        Assert.True(
            compilation.IsSuccess,
            string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        I18nCompiledMessage message = compilation.Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("position", position));

        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Compile_NumberWithoutSelectOption_RemainsCardinal()
    {
        const string source =
            ".input {$count :number}\n" +
            ".match $count\n" +
            "one {{one}}\n" +
            "two {{two}}\n" +
            "* {{other}}";

        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        Assert.Equal(
            "other",
            message.Format(CultureInfo.GetCultureInfo("en"), ("count", 2)).Text);
    }

    [Fact]
    public void Compile_UnsupportedNumberSelectOption_ReturnsDiagnostic()
    {
        const string source =
            ".input {$count :number select=unknown}\n" +
            ".match $count\n" +
            "* {{fallback}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
