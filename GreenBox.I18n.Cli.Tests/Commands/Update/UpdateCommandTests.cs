using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class UpdateCommandTests : IDisposable
{
    private const long EntryId = 3857333080842834967;

    private readonly TestCatalog _catalog = new();

    [Fact]
    public void Execute_CommentTextAndAsset_UpdatesEntryAtomically()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = UpdateCommand.Execute(
            catalogFile,
            EntryId,
            new UpdateRequest
            {
                HasComment = true,
                Comment = "Shown above every report.",
                Locale = "en",
                HasText = true,
                Text = "All reports",
                HasAssetGuid = true,
                AssetGuid = "0123456789abcdef0123456789abcdef",
                HasAssetLocalId = true,
                AssetLocalId = "21300000",
            },
            true,
            standardOutput,
            standardError);

        I18nCatalog saved = I18nCatalogJson.Deserialize(File.ReadAllText(catalogFile.FullName));
        I18nEntry entry = saved.FindById(EntryId)!;
        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Equal("Shown above every report.", entry.Comment);
        Assert.Equal("All reports", entry.Locales["en"].Text);
        Assert.Equal("0123456789abcdef0123456789abcdef", entry.Locales["en"].Asset!.AssetGuid);
        Assert.Equal("21300000", entry.Locales["en"].Asset!.LocalFileId);
        Assert.True(report.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(EntryId.ToString(), report.RootElement.GetProperty("entry").GetProperty("id").GetString());
        Assert.Empty(standardError.ToString());
        Assert.Empty(catalogFile.Directory!.EnumerateFiles("*.tmp"));
    }

    [Fact]
    public void Execute_ClearText_PreservesAssetReference()
    {
        I18nCatalog source = I18nCatalogJson.Deserialize(TestCatalog.ValidJson);
        source.FindById(EntryId)!.Locales["en"].Asset = new I18nAssetReference
        {
            AssetGuid = "0123456789abcdef0123456789abcdef",
        };
        FileInfo catalogFile = _catalog.Write(I18nCatalogJson.Serialize(source));

        int exitCode = UpdateCommand.Execute(
            catalogFile,
            EntryId,
            new UpdateRequest
            {
                Locale = "en",
                ClearText = true,
            },
            false,
            new StringWriter(),
            new StringWriter());

        I18nEntry saved = I18nCatalogJson
            .Deserialize(File.ReadAllText(catalogFile.FullName))
            .FindById(EntryId)!;
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Null(saved.Locales["en"].Text);
        Assert.NotNull(saved.Locales["en"].Asset);
    }

    [Fact]
    public void Execute_InvalidAsset_DoesNotModifyCatalog()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();

        int exitCode = UpdateCommand.Execute(
            catalogFile,
            EntryId,
            new UpdateRequest
            {
                HasComment = true,
                Comment = "This must not be written.",
                Locale = "en",
                HasAssetGuid = true,
                AssetGuid = "invalid",
            },
            true,
            standardOutput,
            new StringWriter());

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal(
            I18nValidationCodes.InvalidAssetGuid,
            report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(originalJson, File.ReadAllText(catalogFile.FullName));
    }

    [Fact]
    public void Execute_TextWithoutLocale_ReturnsErrorWithoutReadingCatalog()
    {
        var standardOutput = new StringWriter();

        int exitCode = UpdateCommand.Execute(
            _catalog.MissingFile,
            EntryId,
            new UpdateRequest
            {
                HasText = true,
                Text = "Text",
            },
            true,
            standardOutput,
            new StringWriter());

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.Equal(
            CliDiagnosticCodes.InvalidUpdate,
            report.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Execute_UnknownLocale_DoesNotModifyCatalog()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        string originalJson = File.ReadAllText(catalogFile.FullName);
        var standardOutput = new StringWriter();

        int exitCode = UpdateCommand.Execute(
            catalogFile,
            EntryId,
            new UpdateRequest
            {
                Locale = "de",
                HasText = true,
                Text = "Berichte",
            },
            true,
            standardOutput,
            new StringWriter());

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal(
            I18nEditCodes.UnknownLocale,
            report.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(originalJson, File.ReadAllText(catalogFile.FullName));
    }

    [Fact]
    public void Execute_SameValue_DoesNotRewriteCatalog()
    {
        FileInfo catalogFile = _catalog.Write(TestCatalog.ValidJson);
        DateTime originalWriteTime = catalogFile.LastWriteTimeUtc;
        var standardOutput = new StringWriter();

        int exitCode = UpdateCommand.Execute(
            catalogFile,
            EntryId,
            new UpdateRequest
            {
                HasComment = true,
                Comment = "Report heading",
            },
            true,
            standardOutput,
            new StringWriter());

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.False(report.RootElement.GetProperty("changed").GetBoolean());
        Assert.Equal(originalWriteTime, catalogFile.LastWriteTimeUtc);
    }

    public void Dispose()
    {
        _catalog.Dispose();
    }
}
