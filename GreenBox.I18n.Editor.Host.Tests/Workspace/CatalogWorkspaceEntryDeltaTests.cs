using GreenBox.I18n.Workspace.Contracts;
using GreenBox.I18n.Workspace;

namespace GreenBox.I18n.Editor.Host.Tests.Workspace;

public sealed class CatalogWorkspaceEntryDeltaTests
{
    [Fact]
    public void ApplyEntryDelta_RestoresRemovedEntryWithSameId()
    {
        CatalogWorkspace session = OpenSession();
        CatalogEditResult addResult = session.AddEntry("Undo.Created");
        CatalogEntryResponse created = Assert.Single(
            addResult.Catalog!.Entries,
            entry => entry.Path == "Undo.Created");

        CatalogEditResult removeResult = session.ApplyEntryDelta(
            addResult.Catalog.Revision,
            Array.Empty<CatalogEntryEditRequest>(),
            new[] { created.Id });
        Assert.DoesNotContain(removeResult.Catalog!.Entries, entry => entry.Id == created.Id);

        CatalogEditResult restoreResult = session.ApplyEntryDelta(
            removeResult.Catalog.Revision,
            new[] { ToEditRequest(created) },
            Array.Empty<string>());
        CatalogEntryResponse restored = Assert.Single(
            restoreResult.Catalog!.Entries,
            entry => entry.Id == created.Id);

        Assert.Equal(created.Path, restored.Path);
    }

    [Fact]
    public void ApplyEntryDelta_InvalidResultDoesNotChangeWorkingCopy()
    {
        CatalogWorkspace session = OpenSession();
        CatalogResponse before = session.GetCatalogSnapshot()!;

        CatalogEditResult result = session.ApplyEntryDelta(
            before.Revision,
            new[]
            {
                new CatalogEntryEditRequest(
                    "3857333080842833240",
                    "Existing.Entry",
                    null,
                    new Dictionary<string, CatalogLocaleValueEditRequest>()),
            },
            Array.Empty<string>());

        Assert.NotNull(result.Error);
        CatalogResponse after = session.GetCatalogSnapshot()!;
        Assert.Equal(before.Revision, after.Revision);
        CatalogEntryResponse beforeEntry = Assert.Single(before.Entries);
        CatalogEntryResponse afterEntry = Assert.Single(after.Entries);
        Assert.Equal(beforeEntry.Id, afterEntry.Id);
        Assert.Equal(beforeEntry.Path, afterEntry.Path);
        Assert.Equal(beforeEntry.Locales.Keys, afterEntry.Locales.Keys);
        Assert.Equal(beforeEntry.Locales["en"].Text, afterEntry.Locales["en"].Text);
    }

    [Fact]
    public void ApplyEntryDelta_StaleRevisionDoesNotChangeWorkingCopy()
    {
        CatalogWorkspace session = OpenSession();
        CatalogResponse before = session.GetCatalogSnapshot()!;
        session.AddEntry("Newer.Entry");
        CatalogResponse current = session.GetCatalogSnapshot()!;

        CatalogEditResult result = session.ApplyEntryDelta(
            before.Revision,
            Array.Empty<CatalogEntryEditRequest>(),
            new[] { "3857333080842832991" });

        Assert.Equal(WorkspaceErrorCodes.CatalogRevisionMismatch, result.Error?.Code);
        CatalogResponse after = session.GetCatalogSnapshot()!;
        Assert.Equal(current.Revision, after.Revision);
        Assert.Contains(after.Entries, entry => entry.Id == "3857333080842832991");
        Assert.Contains(after.Entries, entry => entry.Path == "Newer.Entry");
    }

    [Fact]
    public void ApplyEntryDelta_RestoresCompleteLocalizedEntryContent()
    {
        CatalogWorkspace session = OpenSession();
        CatalogResponse before = session.GetCatalogSnapshot()!;
        CatalogEntryResponse existing = Assert.Single(before.Entries);
        CatalogEditResult removeResult = session.RemoveEntries(new[] { existing.Id });

        CatalogEditResult restoreResult = session.ApplyEntryDelta(
            removeResult.Catalog!.Revision,
            new[] { ToEditRequest(existing) },
            Array.Empty<string>());

        CatalogEntryResponse restored = Assert.Single(restoreResult.Catalog!.Entries);
        Assert.Equal("Existing comment", restored.Comment);
        Assert.Equal("Existing", restored.Locales["en"].Text);
        Assert.Equal("0123456789abcdef0123456789abcdef", restored.Locales["en"].Asset?.AssetGuid);
        Assert.Equal("123456", restored.Locales["en"].Asset?.LocalFileId);
    }

    private static CatalogWorkspace OpenSession()
    {
        var catalog = new I18nCatalog
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
                    Comment = "Existing comment",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new()
                        {
                            Text = "Existing",
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "0123456789abcdef0123456789abcdef",
                                LocalFileId = "123456",
                            },
                        },
                    },
                },
            },
        };

        var session = new CatalogWorkspace();
        session.Open("C:\\catalog.json", catalog, "CONTENT_HASH");
        return session;
    }

    private static CatalogEntryEditRequest ToEditRequest(CatalogEntryResponse entry)
    {
        return new CatalogEntryEditRequest(
            entry.Id,
            entry.Path,
            entry.Comment,
            entry.Locales.ToDictionary(
                pair => pair.Key,
                pair => new CatalogLocaleValueEditRequest(
                    pair.Value.Text,
                    pair.Value.Asset == null
                        ? null
                        : new CatalogAssetReferenceEditRequest(
                            pair.Value.Asset.AssetGuid,
                            pair.Value.Asset.LocalFileId)),
                StringComparer.Ordinal));
    }
}
