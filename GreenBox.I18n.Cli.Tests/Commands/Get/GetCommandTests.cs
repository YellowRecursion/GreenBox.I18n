using System.Text.Json;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class GetCommandTests : IDisposable
{
    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_ExistingId_WritesCompleteEntryAsJson()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GetCommand.Execute(catalogFile, 20, true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        JsonElement entry = report.RootElement.GetProperty("entry");
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal("20", entry.GetProperty("id").GetString());
        Assert.Equal("Reports.Title", entry.GetProperty("path").GetString());
        Assert.Equal("Report heading", entry.GetProperty("comment").GetString());
        Assert.Equal("Reports", entry.GetProperty("locales").GetProperty("en").GetProperty("text").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_MissingId_WritesStableErrorAndReturnsInvalidData()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GetCommand.Execute(catalogFile, 999, true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal("entry_not_found", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_NonPositiveId_ReturnsExecutionErrorWithoutReadingFile()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GetCommand.Execute(_catalog.MissingFile, 0, true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.Equal("invalid_id", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    public void Dispose()
    {
        _catalog.Dispose();
    }
}
