using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageStringMatcherTests
{
    private const string PronounMessage =
        ".input {$pronoun :string}\n" +
        ".match $pronoun\n" +
        "he {{He is ready.}}\n" +
        "she {{She is ready.}}\n" +
        "* {{They are ready.}}";

    [Theory]
    [InlineData("he", "He is ready.")]
    [InlineData("she", "She is ready.")]
    [InlineData("they", "They are ready.")]
    [InlineData("HE", "They are ready.")]
    public void Format_StringSelector_MatchesExactly(string pronoun, string expected)
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile(PronounMessage).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("pronoun", pronoun));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Compile_StringMatcher_ExposesSelectorAsArgument()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(PronounMessage);

        Assert.True(compilation.IsSuccess);
        Assert.Equal(new[] { "pronoun" }, compilation.Message!.ArgumentNames);
    }
}
