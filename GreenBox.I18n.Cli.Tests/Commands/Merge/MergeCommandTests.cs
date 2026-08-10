using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class MergeCommandTests : IDisposable
{
    private readonly TestCatalog _files = new();

    [Fact]
    public void Execute_IndependentChanges_WritesCanonicalMergedCatalog()
    {
        I18nCatalog baseline = Parse(TestCatalog.ValidJson);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries[0].Comment = "Changed locally";
        incoming.Entries[1].Locales["en"].Text = "Incoming reports";
        FileInfo baseFile = Write("base.json", baseline);
        FileInfo currentFile = Write("current.json", current);
        FileInfo incomingFile = Write("incoming.json", incoming);
        FileInfo outputFile = _files.GetFile("merged.json");
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MergeCommand.Execute(
            baseFile,
            currentFile,
            incomingFile,
            outputFile,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        I18nCatalog merged = Parse(File.ReadAllText(outputFile.FullName));
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.True(report.RootElement.GetProperty("merged").GetBoolean());
        Assert.Empty(report.RootElement.GetProperty("conflicts").EnumerateArray());
        Assert.Equal("Changed locally", merged.Entries[0].Comment);
        Assert.Equal("Incoming reports", merged.Entries[1].Locales["en"].Text);
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_ConflictingChange_DoesNotOverwriteCurrentOutput()
    {
        I18nCatalog baseline = Parse(TestCatalog.ValidJson);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries[0].Locales["en"].Text = "Local";
        incoming.Entries[0].Locales["en"].Text = "Incoming";
        FileInfo baseFile = Write("base.json", baseline);
        FileInfo currentFile = Write("current.json", current);
        FileInfo incomingFile = Write("incoming.json", incoming);
        string originalCurrent = File.ReadAllText(currentFile.FullName);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = MergeCommand.Execute(
            baseFile,
            currentFile,
            incomingFile,
            currentFile,
            false,
            standardOutput,
            standardError);

        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal(originalCurrent, File.ReadAllText(currentFile.FullName));
        Assert.Empty(standardOutput.ToString());
        Assert.Contains("$.entries[id=3857333080842832461].locales.en.text", standardError.ToString());
    }

    [Fact]
    public void Execute_MissingInput_DoesNotCreateOutput()
    {
        FileInfo currentFile = _files.Write("current.json", TestCatalog.ValidJson);
        FileInfo incomingFile = _files.Write("incoming.json", TestCatalog.ValidJson);
        FileInfo outputFile = _files.GetFile("merged.json");

        int exitCode = MergeCommand.Execute(
            _files.MissingFile,
            currentFile,
            incomingFile,
            outputFile,
            false,
            new StringWriter(),
            new StringWriter());

        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.False(outputFile.Exists);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private FileInfo Write(string name, I18nCatalog catalog)
    {
        return _files.Write(name, I18nCatalogJson.Serialize(catalog));
    }

    private static I18nCatalog Parse(string json)
    {
        return I18nCatalogJson.Deserialize(json);
    }

    private static I18nCatalog Clone(I18nCatalog catalog)
    {
        return Parse(I18nCatalogJson.Serialize(catalog));
    }
}
