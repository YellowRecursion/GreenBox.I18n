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
        current.Entries.Add(new I18nEntry { Id = "2", Path = "Menu.Settings" });
        incoming.Entries[0].Comment = "External comment";

        I18nCatalogMergeResult result = I18nCatalogMerge.Merge(baseline, current, incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Catalog!.Entries.Count);
        Assert.Equal("External comment", result.Catalog.Entries.Single(entry => entry.Id == "1").Comment);
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
        Assert.Contains(result.Conflicts, conflict => conflict.JsonPath == "$.entries[id=1]");
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
                    Id = "1",
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
}
