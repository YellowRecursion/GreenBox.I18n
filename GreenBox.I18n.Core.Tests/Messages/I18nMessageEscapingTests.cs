using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageEscapingTests
{
    [Fact]
    public void Format_EscapedPatternCharacters_OutputsLiteralCharacters()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(
            @"Curly: \{value\}. Pipe: \|. Slash: \\.");

        Assert.True(compilation.IsSuccess);
        Assert.Equal(
            "Curly: {value}. Pipe: |. Slash: \\.",
            compilation.Message!.Format().Text);
    }

    [Fact]
    public void Compile_UnsupportedEscape_ReturnsSyntaxDiagnostic()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(@"Invalid: \x");

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidEscape,
            Assert.Single(compilation.Diagnostics).Code);
    }

    [Fact]
    public void Format_EscapedBraceAtEndOfQuotedPattern_DoesNotClosePatternEarly()
    {
        const string source = ".input {$name}\n{{{$name}: \\}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.True(compilation.IsSuccess);
        Assert.Equal("Vadim: }", compilation.Message!.Format(("name", "Vadim")).Text);
    }

    [Theory]
    [InlineData("not set", "Unset")]
    [InlineData("a | b", "Pipe")]
    [InlineData("other", "Fallback")]
    public void Format_QuotedStringVariantKey_MatchesCookedValue(string state, string expected)
    {
        const string source =
            ".input {$state :string}\n" +
            ".match $state\n" +
            "|not set| {{Unset}}\n" +
            "|a \\| b| {{Pipe}}\n" +
            "* {{Fallback}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        I18nMessageFormatResult result = message.Format(
            CultureInfo.InvariantCulture,
            ("state", state));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }
}
