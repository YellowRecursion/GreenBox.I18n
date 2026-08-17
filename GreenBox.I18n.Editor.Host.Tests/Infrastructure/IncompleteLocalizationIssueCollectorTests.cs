using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Tests.Infrastructure;

public sealed class IncompleteLocalizationIssueCollectorTests
{
    [Fact]
    public void Enumerate_MatchesWebEditorCompletenessRules()
    {
        CatalogResponse catalog = CreateCatalog(
            Entry("1", "Text.Only", Text("Play"), Text("  ")),
            Entry("2", "Asset.Only", Asset(), null),
            Entry("3", "Mixed.Value", Text("Mixed"), Asset()),
            Entry("4", "Empty.Value", null, null));

        WorkspaceIssueResponse[] issues =
            IncompleteLocalizationIssueCollector.Enumerate(catalog).ToArray();

        Assert.Collection(
            issues,
            issue => AssertIssue(issue, "Text.Only", "missing_locale_text", "ru"),
            issue => AssertIssue(issue, "Asset.Only", "missing_locale_asset", "ru"),
            issue => AssertIssue(issue, "Mixed.Value", "missing_locale_asset", "en"),
            issue => AssertIssue(issue, "Mixed.Value", "missing_locale_text", "ru"),
            issue => AssertIssue(issue, "Empty.Value", "no_localized_content", null));
    }

    private static CatalogResponse CreateCatalog(params CatalogEntryResponse[] entries) =>
        new(
            1,
            "en",
            [
                new CatalogLocaleResponse("en", "English", "en-US", null, null),
                new CatalogLocaleResponse("ru", "Русский", "ru-RU", null, null),
            ],
            entries,
            [],
            [],
            [],
            [],
            false);

    private static CatalogEntryResponse Entry(
        string id,
        string path,
        CatalogLocaleValueResponse? english,
        CatalogLocaleValueResponse? russian)
    {
        var locales = new Dictionary<string, CatalogLocaleValueResponse>(StringComparer.Ordinal);
        if (english != null)
        {
            locales.Add("en", english);
        }

        if (russian != null)
        {
            locales.Add("ru", russian);
        }

        return new CatalogEntryResponse(id, path, null, locales);
    }

    private static CatalogLocaleValueResponse Text(string text) => new(text, null);

    private static CatalogLocaleValueResponse Asset() =>
        new(null, new CatalogAssetReferenceResponse(new string('a', 32), null));

    private static void AssertIssue(
        WorkspaceIssueResponse issue,
        string entryPath,
        string code,
        string? localeId)
    {
        Assert.Equal("incomplete", issue.Kind);
        Assert.Equal("warning", issue.Severity);
        Assert.Equal(entryPath, issue.EntryPath);
        Assert.Equal(code, issue.Code);
        Assert.Equal(localeId, issue.LocaleId);
    }
}
