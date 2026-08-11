using System.Globalization;
using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nMessagePlaceholderOptionsTests
{
    [Fact]
    public void Format_PlaceholderOverridesOneOptionAndInheritsTheRest()
    {
        const string source =
            ".input {$value :number maximumFractionDigits=2 signDisplay=always useGrouping=never}\n" +
            "{{Value: {$value :number maximumFractionDigits=1}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 12.34m)).Text;

        Assert.Equal("Value: +12.3", result);
    }

    [Fact]
    public void Format_SameArgumentCanHaveDifferentPlaceholderOptions()
    {
        const string source =
            ".input {$value :number}\n" +
            "{{{$value :number maximumFractionDigits=0} / " +
            "{$value :number minimumFractionDigits=2 maximumFractionDigits=2}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1.25m)).Text;

        Assert.Equal("1 / 1.25", result);
    }

    [Fact]
    public void Format_AnnotatedPlaceholderWorksWithoutInputDeclaration()
    {
        I18nCompiledMessage message = I18nMessageCompiler
            .Compile("Value: {$value :number maximumFractionDigits=1}")
            .Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1.25m)).Text;

        Assert.Equal("Value: 1.3", result);
    }

    [Fact]
    public void Format_UntypedInputCanBeAnnotatedAtPlaceholder()
    {
        const string source =
            ".input {$value}\n" +
            "{{Value: {$value :number maximumFractionDigits=1}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("ru-RU"),
            ("value", 1.25m)).Text;

        Assert.Equal("Value: 1,3", result);
    }

    [Fact]
    public void Format_PercentPlaceholderInheritsPercentStyle()
    {
        const string source =
            ".input {$value :percent maximumFractionDigits=2}\n" +
            "{{{$value :percent maximumFractionDigits=1}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 0.1234m)).Text;

        Assert.Equal("12.3%", result);
    }

    [Fact]
    public void Format_IntegerPlaceholderOverridesNumberFractionFormatting()
    {
        const string source =
            ".input {$value :number maximumFractionDigits=2}\n" +
            "{{{$value :integer}}}";
        I18nCompiledMessage message = I18nMessageCompiler.Compile(source).Message!;

        string result = message.Format(
            CultureInfo.GetCultureInfo("en-US"),
            ("value", 1.6m)).Text;

        Assert.Equal("2", result);
    }

    [Fact]
    public void Compile_IncompatiblePlaceholderAnnotationReturnsDiagnostic()
    {
        const string source =
            ".input {$value :string}\n" +
            "{{{$value :number}}}";

        I18nMessageCompilation compilation = I18nMessageCompiler.Compile(source);

        Assert.False(compilation.IsSuccess);
        Assert.Equal(
            I18nMessageDiagnosticCodes.InvalidSyntax,
            Assert.Single(compilation.Diagnostics).Code);
    }
}
