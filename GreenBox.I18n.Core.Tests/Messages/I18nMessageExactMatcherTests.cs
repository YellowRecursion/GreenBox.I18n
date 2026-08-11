using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageExactMatcherTests
{
    [Theory]
    [InlineData(1, "exactly one")]
    [InlineData(2, "exactly two")]
    [InlineData(21, "anything else")]
    public void Format_ExactSelector_DoesNotUsePluralCategories(int value, string expected)
    {
        const string source =
            ".input {$count :number select=exact}\n" +
            ".match $count\n" +
            "1 {{exactly one}}\n" +
            "2 {{exactly two}}\n" +
            "* {{anything else}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en"),
            ("count", value)).Text;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compile_ExactSelectorRejectsPluralCategoryKey()
    {
        const string source =
            ".input {$count :number select=exact}\n" +
            ".match $count\n" +
            "one {{wrong}}\n" +
            "* {{fallback}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
