using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class MoveCommandTests : IDisposable
{
    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_ValidMove_WritesCatalogInCanonicalOrderAndReturnsJson()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MoveCommand.Execute(
            catalogFile,
            20,
            "Menu.Entry2",
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        I18nCatalog savedCatalog = I18nCatalogJson.Deserialize(File.ReadAllText(catalogFile.FullName));
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal("20", report.RootElement.GetProperty("id").GetString());
        Assert.Equal("Reports.Title", report.RootElement.GetProperty("previousPath").GetString());
        Assert.Equal("Menu.Entry2", report.RootElement.GetProperty("path").GetString());
        Assert.True(report.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(new[] { "Menu.Entry2", "Menu.History" }, savedCatalog.Entries.Select(entry => entry.Path));
        Assert.Empty(standardError.ToString());
        Assert.Empty(catalogFile.Directory!.EnumerateFiles("*.tmp"));
    }

    [Fact]
    public void Execute_SamePath_DoesNotRewriteCatalog()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MoveCommand.Execute(
            catalogFile,
            20,
            "Reports.Title",
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.False(report.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(originalJson, File.ReadAllText(catalogFile.FullName));
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_DuplicatePath_DoesNotModifyCatalogAndReturnsInvalidData()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MoveCommand.Execute(
            catalogFile,
            20,
            "menu.history",
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal("duplicate_path", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(originalJson, File.ReadAllText(catalogFile.FullName));
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_InvalidPath_ReturnsExecutionErrorWithoutReadingFile()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MoveCommand.Execute(
            _catalog.MissingFile,
            20,
            "Reports..Title",
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.Equal("invalid_path", report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    public void Dispose()
    {
        _catalog.Dispose();
    }
}
