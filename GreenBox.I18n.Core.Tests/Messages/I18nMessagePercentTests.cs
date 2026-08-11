using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessagePercentTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    public void Format_Percent_UsesCultureAndMultipliesByOneHundred(string cultureName)
    {
        const string source = ".input {$value :percent}\n{{{$value}}}";
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(culture, ("value", 0.1234m)).Text;

        Assert.Equal(0.1234m.ToString("P0", culture), result);
    }

    [Fact]
    public void Format_Percent_FractionOptionsApplyAfterScaling()
    {
        const string source =
            ".input {$value :percent maximumFractionDigits=1}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 0.1234m)).Text;

        Assert.Equal("12.3%", result);
    }

    [Theory]
    [InlineData(0.014, "one")]
    [InlineData(0.015, "other")]
    public void Format_Percent_SelectsPluralFromFormattedValue(double value, string expected)
    {
        const string source =
            ".input {$value :percent}\n" +
            ".match $value\n" +
            "one {{one}}\n" +
            "* {{other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_Percent_ReusesSignDisplay()
    {
        const string source =
            ".input {$value :percent signDisplay=always maximumFractionDigits=1}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 0.125m)).Text;

        Assert.Equal("+12.5%", result);
    }

    [Theory]
    [InlineData("eu-ES")]
    [InlineData("fa-IR")]
    [InlineData("nqo-GN")]
    public void Format_NegativePercent_UsesCultureNegativePattern(string cultureName)
    {
        const string source = ".input {$value :percent}\n{{{$value}}}";
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(culture, ("value", -0.12m)).Text;

        Assert.Equal((-0.12m).ToString("P0", culture), result);
    }

    [Theory]
    [InlineData(0, "-12 %")]
    [InlineData(1, "-12%")]
    [InlineData(2, "-%12")]
    [InlineData(3, "%-12")]
    [InlineData(4, "%12-")]
    [InlineData(5, "12-%")]
    [InlineData(6, "12%-")]
    [InlineData(7, "-% 12")]
    [InlineData(8, "12 %-")]
    [InlineData(9, "% 12-")]
    [InlineData(10, "% -12")]
    [InlineData(11, "12- %")]
    public void Format_NegativePercent_SupportsEveryRuntimePattern(int pattern, string expected)
    {
        const string source = ".input {$value :percent}\n{{{$value}}}";
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PercentNegativePattern = pattern;
        culture.NumberFormat.PercentPositivePattern = 1;
        culture.NumberFormat.PercentSymbol = "%";
        culture.NumberFormat.NegativeSign = "-";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(culture, ("value", -0.12m)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_PercentOverflow_ReturnsReadableFallbackAndDiagnostic()
    {
        const string source = ".input {$value :percent}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", decimal.MaxValue));

        Assert.Equal("{$value}", result.Text);
        Assert.False(result.IsSuccess);
        I18nMessageDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nMessageDiagnosticCodes.UnsupportedOperation, diagnostic.Code);
        Assert.Equal("value", diagnostic.ArgumentName);
    }

    [Fact]
    public void Format_PercentSelectorOverflow_ReturnsReadableFallbackAndDiagnostic()
    {
        const string source =
            ".input {$value :percent}\n" +
            ".match $value\n" +
            "* {{fallback}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", decimal.MaxValue));

        Assert.Equal("{$value}", result.Text);
        Assert.False(result.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.UnsupportedOperation,
            Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("select=ordinal")]
    [InlineData("minimumIntegerDigits=2")]
    [InlineData("roundingIncrement=5")]
    public void Compile_PercentRejectsUnsupportedOptions(string options)
    {
        string source = ".input {$value :percent " + options + "}\n{{{$value}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
    }
}
