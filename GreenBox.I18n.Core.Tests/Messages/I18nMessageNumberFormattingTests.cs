using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageNumberFormattingTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Format_NumberInput_UsesMessageCulture(string cultureName)
    {
        const string source = ".input {$count :number}\n{{Total: {$count}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo(cultureName),
            ("count", 1234.5m));

        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        string expected = "Total: " + 1234.5m.ToString("#,##0.0", culture);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("en", "12.35")]
    [InlineData("ru", "12,35")]
    public void Format_FractionDigitOptions_RoundAndPad(string cultureName, string expected)
    {
        const string source =
            ".input {$value :number minimumFractionDigits=2 maximumFractionDigits=2}\n" +
            "{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo(cultureName),
            ("value", 12.345m));

        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("useGrouping=never", "12345.5")]
    [InlineData("useGrouping=always", "12,345.5")]
    [InlineData("useGrouping=min2", "12,345.5")]
    public void Format_GroupingOption_IsApplied(string option, string expected)
    {
        string source = ".input {$value :number " + option + "}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 12345.5m)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_Min2Grouping_DoesNotGroupSingleLeadingDigit()
    {
        const string source =
            ".input {$value :number useGrouping=min2}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 1234m)).Text;

        Assert.Equal("1234", result);
    }

    [Theory]
    [InlineData("always", 12.5, "+12.5")]
    [InlineData("exceptZero", 12.5, "+12.5")]
    [InlineData("exceptZero", 0, "0")]
    [InlineData("never", -12.5, "12.5")]
    public void Format_SignDisplayOption_IsApplied(
        string signDisplay,
        double value,
        string expected)
    {
        string source =
            ".input {$value :number signDisplay=" + signDisplay + "}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_RoundedNumber_IsAlsoUsedForPluralSelection()
    {
        const string source =
            ".input {$value :number maximumFractionDigits=0}\n" +
            ".match $value\n" +
            "one {{one}}\n" +
            "* {{other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 1.2m)).Text;

        Assert.Equal("one", result);
    }

    [Fact]
    public void Format_MinimumFractionDigits_IsAlsoUsedForPluralSelection()
    {
        const string source =
            ".input {$value :number minimumFractionDigits=1}\n" +
            ".match $value\n" +
            "one {{one}}\n" +
            "* {{other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 1m)).Text;

        Assert.Equal("other", result);
    }

    [Fact]
    public void Format_NegativeSignDisplay_HidesNegativeZero()
    {
        const string source =
            ".input {$value :number signDisplay=negative}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;
        decimal negativeZero = new decimal(0, 0, 0, true, 0);

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", negativeZero)).Text;

        Assert.Equal("0", result);
    }

    [Fact]
    public void Format_MinimumIntegerDigits_PadsWithZeros()
    {
        const string source =
            ".input {$value :number minimumIntegerDigits=4 useGrouping=never}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 12m)).Text;

        Assert.Equal("0012", result);
    }

    [Fact]
    public void Format_StripTrailingZerosForInteger()
    {
        const string source =
            ".input {$value :number minimumFractionDigits=2 trailingZeroDisplay=stripIfInteger}\n" +
            "{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string integer = message.Format(CultureInfo.GetCultureInfo("en"), ("value", 12m)).Text;
        string fraction = message.Format(CultureInfo.GetCultureInfo("en"), ("value", 12.5m)).Text;

        Assert.Equal("12", integer);
        Assert.Equal("12.50", fraction);
    }

    [Theory]
    [InlineData("ceil", 1.21, "1.3")]
    [InlineData("floor", 1.29, "1.2")]
    [InlineData("expand", -1.21, "-1.3")]
    [InlineData("trunc", -1.29, "-1.2")]
    [InlineData("halfCeil", -1.25, "-1.2")]
    [InlineData("halfFloor", 1.25, "1.2")]
    [InlineData("halfExpand", -1.25, "-1.3")]
    [InlineData("halfTrunc", -1.25, "-1.2")]
    [InlineData("halfEven", 1.25, "1.2")]
    public void Format_RoundingMode_IsApplied(string mode, double value, string expected)
    {
        string source =
            ".input {$value :number maximumFractionDigits=1 roundingMode=" + mode +
            " useGrouping=never}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("maximumSignificantDigits=3", 12345, "12,300")]
    [InlineData("minimumSignificantDigits=4", 12, "12.00")]
    [InlineData("maximumSignificantDigits=3", 0.012345, "0.0123")]
    public void Format_SignificantDigitOptions_AreApplied(
        string options,
        double value,
        string expected)
    {
        string source = ".input {$value :number " + options + "}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", (decimal)value)).Text;

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("morePrecision", "4.32")]
    [InlineData("lessPrecision", "4.3")]
    public void Format_RoundingPriority_ChoosesRequestedPrecision(string priority, string expected)
    {
        string source =
            ".input {$value :number maximumFractionDigits=2 maximumSignificantDigits=2 " +
            "roundingPriority=" + priority + "}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 4.321m)).Text;

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("morePrecision", "1.0")]
    [InlineData("lessPrecision", "1.00")]
    public void Format_RoundingPriority_UsesRoundingMagnitudeWhenRoundedValuesAreEqual(
        string priority,
        string expected)
    {
        string source =
            ".input {$value :number minimumFractionDigits=2 maximumFractionDigits=2 " +
            "minimumSignificantDigits=2 maximumSignificantDigits=6 roundingPriority=" +
            priority + "}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1m)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_RoundingIncrement_RoundsAtMaximumFractionScale()
    {
        const string source =
            ".input {$value :number minimumFractionDigits=2 maximumFractionDigits=2 roundingIncrement=5}\n{{{$value}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 1.23m)).Text;

        Assert.Equal("1.25", result);
    }

    [Fact]
    public void Format_SignificantDigitRounding_IsUsedForPluralSelection()
    {
        const string source =
            ".input {$value :number maximumSignificantDigits=1}\n" +
            ".match $value\n" +
            "one {{one}}\n" +
            "* {{other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("value", 1.2m)).Text;

        Assert.Equal("one", result);
    }

    [Theory]
    [InlineData("minimumFractionDigits=3 maximumFractionDigits=2")]
    [InlineData("minimumFractionDigits=-1")]
    [InlineData("useGrouping=sometimes")]
    [InlineData("signDisplay=positive")]
    [InlineData("minimumIntegerDigits=0")]
    [InlineData("trailingZeroDisplay=strip")]
    [InlineData("roundingMode=nearest")]
    [InlineData("minimumSignificantDigits=0")]
    [InlineData("minimumSignificantDigits=4 maximumSignificantDigits=3")]
    [InlineData("roundingPriority=maximum")]
    [InlineData("roundingIncrement=3 maximumFractionDigits=2")]
    [InlineData("roundingIncrement=5")]
    [InlineData("maximumFractionDigits=2 roundingIncrement=5")]
    [InlineData("minimumFractionDigits=1 maximumFractionDigits=2 roundingIncrement=5")]
    public void Compile_InvalidNumberOptions_ReturnDiagnostic(string options)
    {
        string source = ".input {$value :number " + options + "}\n{{{$value}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }

    [Fact]
    public void Format_NumberInsideSelectedVariant_UsesMessageCulture()
    {
        const string source =
            ".input {$count :number}\n" +
            ".match $count\n" +
            "one {{{$count} предмет}}\n" +
            "few {{{$count} предмета}}\n" +
            "many {{{$count} предметов}}\n" +
            "* {{{$count} предмета}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("ru"),
            ("count", 1.5m));

        Assert.Equal("1,5 предмета", result.Text);
    }

    [Fact]
    public void Format_UntypedNumber_KeepsInvariantFormatting()
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile("Value: {$value}").Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("ru"),
            ("value", 1.5m));

        Assert.Equal("Value: 1.5", result.Text);
    }
}
