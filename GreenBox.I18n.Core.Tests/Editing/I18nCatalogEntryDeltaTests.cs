namespace GreenBox.I18n.Core.Tests.Editing;

public sealed class I18nCatalogEntryDeltaTests
{
    [Fact]
    public void ApplyEntryDelta_RestoresCompleteEntryWithStableId()
    {
        I18nCatalog catalog = CreateCatalog();
        I18nEntry original = catalog.Entries.Single();

        I18nBatchEditResult removeResult = catalog.ApplyEntryDelta(
            Array.Empty<I18nEntry>(),
            new[] { 123L });
        I18nBatchEditResult restoreResult = catalog.ApplyEntryDelta(
            new[] { original },
            Array.Empty<long>());

        Assert.True(removeResult.IsSuccess);
        Assert.True(restoreResult.IsSuccess);
        I18nEntry restored = Assert.Single(catalog.Entries);
        Assert.Equal("123", restored.Id);
        Assert.Equal("Existing.Entry", restored.Path);
        Assert.Equal("Existing", restored.Locales["en"].Text);
        Assert.Equal("0123456789abcdef0123456789abcdef", restored.Locales["en"].Asset?.AssetGuid);
    }

    [Fact]
    public void ApplyEntryDelta_InvalidFinalCatalogIsAtomic()
    {
        I18nCatalog catalog = CreateCatalog();
        var duplicate = new I18nEntry
        {
            Id = "456",
            Path = "Existing.Entry",
            Locales = new Dictionary<string, I18nLocaleValue>(),
        };

        I18nBatchEditResult result = catalog.ApplyEntryDelta(
            new[] { duplicate },
            Array.Empty<long>());

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.InvalidEntryDelta, result.Error?.Code);
        I18nEntry unchanged = Assert.Single(catalog.Entries);
        Assert.Equal("123", unchanged.Id);
    }

    [Fact]
    public void ApplyEntryDelta_RejectsReplacingAndRemovingSameId()
    {
        I18nCatalog catalog = CreateCatalog();

        I18nBatchEditResult result = catalog.ApplyEntryDelta(
            new[] { catalog.Entries.Single() },
            new[] { 123L });

        Assert.False(result.IsSuccess);
        Assert.Equal(I18nEditCodes.InvalidId, result.Error?.Code);
        Assert.Single(catalog.Entries);
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
                    Id = "123",
                    Path = "Existing.Entry",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new()
                        {
                            Text = "Existing",
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "0123456789abcdef0123456789abcdef",
                            },
                        },
                    },
                },
            },
        };
    }
}
