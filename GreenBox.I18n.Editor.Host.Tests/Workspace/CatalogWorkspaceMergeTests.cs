using GreenBox.I18n.Workspace.Contracts;
using GreenBox.I18n.Workspace;

namespace GreenBox.I18n.Editor.Host.Tests.Workspace;

public sealed class CatalogWorkspaceMergeTests
{
    [Fact]
    public void MergeSource_IndependentWebAndDiskChanges_UpdatesWorkingCopyAndBaseline()
    {
        I18nCatalog baseline = CreateCatalog();
        I18nCatalog incoming = Clone(baseline);
        var session = new CatalogWorkspace();
        session.Open("C:\\catalog.json", baseline, "BASE_HASH");
        session.AddEntry("Local.Entry");
        incoming.Entries[0].Locales["en"].Text = "Changed on disk";

        CatalogSourceMergeResult result = session.MergeSource(incoming, "INCOMING_HASH");

        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Conflicts);
        Assert.Contains(result.Catalog.Entries, entry => entry.Path == "Local.Entry");
        CatalogEntryResponse existing = Assert.Single(
            result.Catalog.Entries,
            entry => entry.Path == "Existing.Entry");
        Assert.Equal("Changed on disk", existing.Locales["en"].Text);
        Assert.True(result.Catalog.HasChanges);
    }

    [Fact]
    public void MergeSource_ConflictingField_PreservesWebWorkingCopy()
    {
        I18nCatalog baseline = CreateCatalog();
        I18nCatalog incoming = Clone(baseline);
        var session = new CatalogWorkspace();
        session.Open("C:\\catalog.json", baseline, "BASE_HASH");
        CatalogResponse opened = session.GetCatalogSnapshot()!;
        CatalogEntryResponse entry = Assert.Single(opened.Entries);
        session.ApplyEntryDelta(
            opened.Revision,
            new[]
            {
                new CatalogEntryEditRequest(
                    entry.Id,
                    entry.Path,
                    "Changed in Web",
                    entry.Locales.ToDictionary(
                        pair => pair.Key,
                        pair => new CatalogLocaleValueEditRequest(pair.Value.Text, null))),
            },
            Array.Empty<string>());
        incoming.Entries[0].Comment = "Changed on disk";
        CatalogResponse beforeMerge = session.GetCatalogSnapshot()!;

        CatalogSourceMergeResult result = session.MergeSource(incoming, "INCOMING_HASH");

        Assert.Null(result.Catalog);
        Assert.Contains(result.Conflicts, conflict => conflict.JsonPath.EndsWith(".comment"));
        CatalogResponse afterMerge = session.GetCatalogSnapshot()!;
        Assert.Equal(beforeMerge.Revision, afterMerge.Revision);
        Assert.Equal("Changed in Web", Assert.Single(afterMerge.Entries).Comment);
    }

    private static I18nCatalog CreateCatalog()
    {
        return new I18nCatalog
        {
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new() { Id = "en", DisplayName = "English", Culture = "en" },
            },
            Entries = new List<I18nEntry>
            {
                new()
                {
                    Id = "3857333080842832991",
                    Path = "Existing.Entry",
                    Comment = "Original comment",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new() { Text = "Original text" },
                    },
                },
            },
        };
    }

    private static I18nCatalog Clone(I18nCatalog catalog)
    {
        return I18nCatalogJson.Deserialize(I18nCatalogJson.Serialize(catalog));
    }
}
