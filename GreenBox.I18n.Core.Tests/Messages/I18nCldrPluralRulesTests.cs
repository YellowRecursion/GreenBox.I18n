using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCldrPluralRulesTests
{
    [Theory]
    [InlineData("ar", 0, "zero")]
    [InlineData("ar", 1, "one")]
    [InlineData("ar", 2, "two")]
    [InlineData("ar", 3, "few")]
    [InlineData("ar", 11, "many")]
    [InlineData("ar", 100, "other")]
    [InlineData("pl", 1, "one")]
    [InlineData("pl", 2, "few")]
    [InlineData("pl", 5, "many")]
    [InlineData("cs", 1, "one")]
    [InlineData("cs", 2, "few")]
    [InlineData("cs", 5, "other")]
    [InlineData("fr", 0, "one")]
    [InlineData("fr", 2, "other")]
    public void Format_CldrCardinalRule_SelectsExpectedCategory(
        string cultureName,
        int count,
        string expected)
    {
        I18nCompiledMessage message = CompileCategoryEcho();

        string result = message.Format(
            CultureInfo.GetCultureInfo(cultureName),
            ("count", count)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_RegionalCulture_UsesExactRuleBeforeLanguageRule()
    {
        I18nCompiledMessage message = CompileCategoryEcho();

        string brazil = message.Format(
            CultureInfo.GetCultureInfo("pt-BR"),
            ("count", 0)).Text;
        string portugal = message.Format(
            CultureInfo.GetCultureInfo("pt-PT"),
            ("count", 0)).Text;

        Assert.Equal("one", brazil);
        Assert.Equal("other", portugal);
    }

    [Fact]
    public void Format_Decimal_PreservesVisibleFractionDigitsForCldrOperands()
    {
        I18nCompiledMessage message = CompileCategoryEcho();

        string integer = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("count", 1m)).Text;
        string visibleFraction = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("count", 1.0m)).Text;
        string czechFraction = message.Format(
            CultureInfo.GetCultureInfo("cs"),
            ("count", 1.5m)).Text;

        Assert.Equal("one", integer);
        Assert.Equal("other", visibleFraction);
        Assert.Equal("many", czechFraction);
    }

    private static I18nCompiledMessage CompileCategoryEcho()
    {
        const string source =
            ".input {$count :number}\n" +
            ".match $count\n" +
            "zero {{zero}}\n" +
            "one {{one}}\n" +
            "two {{two}}\n" +
            "few {{few}}\n" +
            "many {{many}}\n" +
            "* {{other}}";
        return I18nMessageCompiler.Compile(source).Message!;
    }
}
