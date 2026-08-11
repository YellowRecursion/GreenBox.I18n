using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageUnicodeTests
{
    [Fact]
    public void Format_ArgumentName_UsesNfcEquivalence()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("Hello {$caf\u00e9}")
            .Message!;

        I18nMessageFormatResult result = message.Format(("cafe\u0301", "Vadim"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello Vadim", result.Text);
        Assert.Equal("caf\u00e9", Assert.Single(message.ArgumentNames));
    }

    [Fact]
    public void Compile_NfcEquivalentArgumentDeclarations_AreDuplicates()
    {
        const string source =
            ".input {$caf\u00e9 :string}\n" +
            ".input {$cafe\u0301 :string}\n" +
            "{{{$caf\u00e9}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
    }

    [Fact]
    public void Format_StringSelector_UsesNfcEquivalence()
    {
        const string source =
            ".input {$value :string}\n" +
            ".match $value\n" +
            "|caf\u00e9| {{Matched}}\n" +
            "* {{Other}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(("value", "cafe\u0301")).Text;

        Assert.Equal("Matched", result);
    }

    [Fact]
    public void Compile_NfcEquivalentStringKeys_AreDuplicates()
    {
        const string source =
            ".input {$value :string}\n" +
            ".match $value\n" +
            "|caf\u00e9| {{First}}\n" +
            "|cafe\u0301| {{Second}}\n" +
            "* {{Other}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
    }
}
