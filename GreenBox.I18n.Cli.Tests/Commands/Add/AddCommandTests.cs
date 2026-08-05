using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class AddCommandTests : IDisposable
{
    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_ValidPath_AllocatesRandomIdAndReturnsJson()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = AddCommand.Execute(
            catalogFile,
            "Menu.PlayButton",
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        I18nCatalog savedCatalog = I18nCatalogJson.Deserialize(File.ReadAllText(catalogFile.FullName));
        I18nEntry addedEntry = Assert.Single(savedCatalog.Entries, entry => entry.Path == "Menu.PlayButton");
        Assert.Equal(CliExitCodes.Success, exitCode);
        string id = report.RootElement.GetProperty("id").GetString()!;
        Assert.InRange(long.Parse(id), 100_000_000_000, 999_999_999_999);
        Assert.Equal("Menu.PlayButton", report.RootElement.GetProperty("path").GetString());
        Assert.Equal(id, addedEntry.Id);
        Assert.Empty(addedEntry.Locales);
        Assert.DoesNotContain("nextId", File.ReadAllText(catalogFile.FullName));
        Assert.Empty(standardError.ToString());
        Assert.Empty(catalogFile.Directory!.EnumerateFiles("*.tmp"));
    }

    [Fact]
    public void Execute_DuplicatePath_DoesNotModifyCatalogAndReturnsInvalidData()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = AddCommand.Execute(
            catalogFile,
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

        int exitCode = AddCommand.Execute(
            _catalog.MissingFile,
            "Menu..PlayButton",
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
