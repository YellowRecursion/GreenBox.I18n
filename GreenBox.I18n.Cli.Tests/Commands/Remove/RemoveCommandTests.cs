using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class RemoveCommandTests : IDisposable
{
    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_ExistingId_RemovesEntryAndPreservesConsumedIds()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = RemoveCommand.Execute(
            catalogFile,
            20,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        I18nCatalog savedCatalog = I18nCatalogJson.Deserialize(File.ReadAllText(catalogFile.FullName));
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal("20", report.RootElement.GetProperty("id").GetString());
        Assert.Equal("Reports.Title", report.RootElement.GetProperty("path").GetString());
        Assert.Equal("21", savedCatalog.NextId);
        Assert.DoesNotContain(savedCatalog.Entries, entry => entry.Id == "20");
        Assert.Empty(standardError.ToString());
        Assert.Empty(catalogFile.Directory!.EnumerateFiles("*.tmp"));
    }

    [Fact]
    public void Execute_MissingId_DoesNotModifyCatalogAndReturnsInvalidData()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = RemoveCommand.Execute(
            catalogFile,
            999,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal("entry_not_found", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(originalJson, File.ReadAllText(catalogFile.FullName));
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_InvalidId_ReturnsExecutionErrorWithoutReadingFile()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = RemoveCommand.Execute(
            _catalog.MissingFile,
            0,
            true,
            standardOutput,
            standardError);

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
