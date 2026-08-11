using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageMultipleMatcherTests
{
    private const string Message =
        ".input {$gender :string}\n" +
        ".input {$count :number}\n" +
        ".match $gender $count\n" +
        "male one {{He has one item.}}\n" +
        "male * {{He has {$count} items.}}\n" +
        "female one {{She has one item.}}\n" +
        "female * {{She has {$count} items.}}\n" +
        "* one {{They have one item.}}\n" +
        "* * {{They have {$count} items.}}";

    [Theory]
    [InlineData("male", 1, "He has one item.")]
    [InlineData("male", 3, "He has 3 items.")]
    [InlineData("female", 1, "She has one item.")]
    [InlineData("female", 3, "She has 3 items.")]
    [InlineData("unknown", 1, "They have one item.")]
    [InlineData("unknown", 3, "They have 3 items.")]
    public void Format_TwoSelectors_SelectsMostSpecificVariant(
        string gender,
        int count,
        string expected)
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile(Message).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("gender", gender),
            ("count", count));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Compile_MultipleSelectors_RequiresSameNumberOfVariantKeys()
    {
        const string invalid =
            ".input {$gender :string}\n" +
            ".input {$count :number}\n" +
            ".match $gender $count\n" +
            "male {{Invalid.}}\n" +
            "* * {{Fallback.}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(invalid);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.VariantKeyMismatch,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
