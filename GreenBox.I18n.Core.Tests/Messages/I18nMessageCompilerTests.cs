using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessageCompilerTests
{
    [Fact]
    public void Compile_PlainText_FormatsWithoutChanges()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile("Hello, world!");

        Assert.True(compilation.IsSuccess);
        Assert.NotNull(compilation.Message);
        Assert.Empty(compilation.Message.ArgumentNames);
        Assert.Equal("Hello, world!", compilation.Message.Format().Text);
    }

    [Fact]
    public void Compile_SimpleMessageWithVariable_SubstitutesArgument()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile("Hello, {$name}!");

        I18nMessageFormatResult result = compilation.Message!.Format(("name", "Vadim"));

        Assert.True(compilation.IsSuccess);
        Assert.Equal(new[] { "name" }, compilation.Message.ArgumentNames);
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, Vadim!", result.Text);
    }

    [Fact]
    public void Compile_InputDeclaration_SubstitutesExternalArgumentInQuotedPattern()
    {
        const string source = ".input {$name}\n{{Hello, {$name}!}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);
        I18nMessageFormatResult result = compilation.Message!.Format(("name", "Vadim"));

        Assert.True(compilation.IsSuccess);
        Assert.Equal(new[] { "name" }, compilation.Message.ArgumentNames);
        Assert.Equal("Hello, Vadim!", result.Text);
    }

    [Fact]
    public void Compile_InputDeclarations_ExposeArgumentKindsForAuthoringTools()
    {
        const string source =
            ".input {$label :string}\n" +
            ".input {$count :number}\n" +
            "{{{$label}: {$count}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.True(compilation.IsSuccess);
        Assert.Equal(new[] { "label", "count" }, compilation.Message!.ArgumentNames);
        Assert.Equal(
            new[] { I18nMessageArgumentKind.String, I18nMessageArgumentKind.Number },
            compilation.Message.ArgumentKinds);
    }

    [Fact]
    public void Compile_RepeatedVariable_ListsArgumentOnlyOnce()
    {
        I18nMessageCompilation compilation = I18nMessageCompiler.Compile("{$name}, {$name}!");

        Assert.Equal(new[] { "name" }, compilation.Message!.ArgumentNames);
        Assert.Equal("Vadim, Vadim!", compilation.Message.Format(("name", "Vadim")).Text);
    }

    [Fact]
    public void Format_TwoTypedTupleArguments_SubstitutesBothValues()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("{$name} has {$count} items.")
            .Message!;

        I18nMessageFormatResult result = message.Format(("name", "Vadim"), ("count", 5));

        Assert.True(result.IsSuccess);
        Assert.Equal("Vadim has 5 items.", result.Text);
    }

    [Fact]
    public void Format_ThreeTypedTupleArguments_SubstitutesEveryValue()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("{$name}: {$count} {$item}.")
            .Message!;

        I18nMessageFormatResult result = message.Format(
            ("name", "Vadim"),
            ("count", 5),
            ("item", "items"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Vadim: 5 items.", result.Text);
    }

    [Fact]
    public void Format_EightTypedTupleArguments_SubstitutesEveryValue()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("{$a}{$b}{$c}{$d}{$e}{$f}{$g}{$h}")
            .Message!;

        I18nMessageFormatResult result = message.Format(
            ("a", 1), ("b", 2), ("c", 3), ("d", 4),
            ("e", 5), ("f", 6), ("g", 7), ("h", 8));

        Assert.True(result.IsSuccess);
        Assert.Equal("12345678", result.Text);
    }

    [Fact]
    public void Format_MoreThanEightTupleArguments_UsesFallback()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("{$a}{$b}{$c}{$d}{$e}{$f}{$g}{$h}{$i}")
            .Message!;

        I18nMessageFormatResult result = message.Format(
            ("a", 1), ("b", 2), ("c", 3), ("d", 4), ("e", 5),
            ("f", 6), ("g", 7), ("h", 8), ("i", 9));

        Assert.True(result.IsSuccess);
        Assert.Equal("123456789", result.Text);
    }

    [Fact]
    public void Format_MissingArgument_PreservesPlaceholderAndReportsDiagnostic()
    {
        I18nCompiledMessage message = I18nMessageCompiler.Compile("Hello, {$name}!").Message!;

        I18nMessageFormatResult result = message.Format();

        Assert.False(result.IsSuccess);
        Assert.Equal("Hello, {$name}!", result.Text);
        I18nMessageDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nMessageDiagnosticCodes.MissingArgument, diagnostic.Code);
        Assert.Equal("name", diagnostic.ArgumentName);
    }
}
