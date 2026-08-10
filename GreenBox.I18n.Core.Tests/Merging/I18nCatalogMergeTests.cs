using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogMergeTests
{
    [Fact]
    public void Merge_DifferentEntryFields_CombinesBothChanges()
    {
        I18nCatalog baseline = CreateCatalog("Original", "Comment");
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries[0].Comment = "Local comment";
        incoming.Entries[0].Locales["en"].Text = "External text";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal("Local comment", result.Catalog!.Entries[0].Comment);
        Assert.Equal("External text", result.Catalog.Entries[0].Locales["en"].Text);
    }

    [Fact]
    public void Merge_SameFieldChangedDifferently_ReturnsConflict()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries[0].Locales["en"].Text = "Local";
        incoming.Entries[0].Locales["en"].Text = "External";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Conflicts, conflict => conflict.JsonPath.EndsWith(".text"));
    }

    [Fact]
    public void Merge_LocalAdditionAndExternalEdit_CombinesBothChanges()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries.Add(new I18nEntry { Id = "3857333080842830453", Path = "Menu.Settings" });
        incoming.Entries[0].Comment = "External comment";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Catalog!.Entries.Count);
        Assert.Equal("External comment", result.Catalog.Entries.Single(entry => entry.Id == "3857333080842830204").Comment);
    }

    [Fact]
    public void Merge_DeletionAndExternalEdit_ReturnsConflict()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries.Clear();
        incoming.Entries[0].Comment = "External comment";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Conflicts, conflict => conflict.JsonPath == "$.entries[id=3857333080842830204]");
    }

    [Fact]
    public void Merge_IndependentLocaleAdditions_PreservesBothWithoutAnOrderConflict()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        AddLocale(current, "de", "Deutsch", "de-DE", "Aktuell");
        AddLocale(incoming, "fr", "Français", "fr-FR", "Actuel");

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "en", "de", "fr" }, result.Catalog!.Locales.Select(locale => locale.Id));
        Assert.Equal("Aktuell", result.Catalog.Entries[0].Locales["de"].Text);
        Assert.Equal("Actuel", result.Catalog.Entries[0].Locales["fr"].Text);
    }

    [Fact]
    public void Merge_InvalidInput_ReturnsConflictInsteadOfThrowingDuringIndexing()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        current.Entries.Add(Clone(current).Entries[0]);

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Conflicts, conflict => conflict.Message.StartsWith("Current catalog is invalid:"));
    }

    [Fact]
    public void Merge_FileCommentChangedOnOneSide_PreservesTheChange()
    {
        I18nCatalog baseline = CreateCatalog("Original", null);
        I18nCatalog current = Clone(baseline);
        I18nCatalog incoming = Clone(baseline);
        incoming.FileComment = "Updated editing instructions";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated editing instructions", result.Catalog!.FileComment);
    }

    private static I18nCatalog CreateCatalog(string text, string? comment)
    {
        return new I18nCatalog
        {
            SchemaVersion = 1,
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new() { Id = "en", DisplayName = "English", Culture = "en-US" },
            },
            Entries = new List<I18nEntry>
            {
                new()
                {
                    Id = "3857333080842830204",
                    Path = "Menu.Play",
                    Comment = comment,
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new() { Text = text },
                    },
                },
            },
        };
    }

    private static I18nCatalog Clone(I18nCatalog catalog)
    {
        return I18nCatalogJson.Deserialize(I18nCatalogJson.Serialize(catalog));
    }

    private static void AddLocale(
        I18nCatalog catalog,
        string id,
        string displayName,
        string culture,
        string text)
    {
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = id,
            DisplayName = displayName,
            Culture = culture,
        });
        catalog.Entries[0].Locales.Add(id, new I18nLocaleValue { Text = text });
    }
}
