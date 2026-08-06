using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class GenerateIdCommandTests
{
    [Fact]
    public void Execute_DefaultOutput_WritesOneValidId()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GenerateIdCommand.Execute(1, false, standardOutput, standardError);

        string id = standardOutput.ToString().Trim();
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.True(I18nEntryId.TryParse(id, out _));
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_CountAndJson_WritesUniqueStringIds()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GenerateIdCommand.Execute(3, true, standardOutput, standardError);

        using JsonDocument document = JsonDocument.Parse(standardOutput.ToString());
        JsonElement ids = document.RootElement.GetProperty("ids");
        string[] values = ids.EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(3, document.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(3, values.Distinct().Count());
        Assert.All(values, value => Assert.True(I18nEntryId.TryParse(value, out _)));
        Assert.Empty(standardError.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Execute_InvalidCount_ReturnsExecutionError(int count)
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GenerateIdCommand.Execute(count, false, standardOutput, standardError);

        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.Empty(standardOutput.ToString());
        Assert.Contains("Count must be between", standardError.ToString());
    }
}
