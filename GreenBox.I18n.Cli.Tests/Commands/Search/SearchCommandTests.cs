using System.Text.Json;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class SearchCommandTests : IDisposable
{
    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_MatchesAndRanksEntriesAsJson()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = SearchCommand.Execute(catalogFile, "reports", true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        JsonElement entries = report.RootElement.GetProperty("entries");
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(2, report.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("20", entries[0].GetProperty("id").GetString());
        Assert.Equal("10", entries[1].GetProperty("id").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_NoMatches_WritesEmptyResultAndReturnsSuccess()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = SearchCommand.Execute(catalogFile, "Settings", true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal(0, report.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(0, report.RootElement.GetProperty("entries").GetArrayLength());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_InvalidCatalog_ReturnsInvalidCatalogError()
    {
        FileInfo catalogFile = _catalog.Write(
            """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "id": "0",
                  "path": "Invalid..Path",
                  "locales": {}
                }
              ]
            }
            """);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = SearchCommand.Execute(catalogFile, "Invalid", true, standardOutput, standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal("invalid_catalog", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    public void Dispose()
    {
        _catalog.Dispose();
    }
}
