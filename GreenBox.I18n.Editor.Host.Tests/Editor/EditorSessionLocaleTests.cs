using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Tests.Editor;

public sealed class EditorSessionLocaleTests
{
    [Fact]
    public void ApplyLocales_RemovalDeletesValuesAndAllowsDefaultReplacement()
    {
        EditorSession session = OpenSession();
        CatalogResponse before = session.GetCatalogSnapshot()!;

        CatalogEditResult result = session.ApplyLocales(
            before.Revision,
            "ru",
            new[]
            {
                new CatalogLocaleEditRequest("ru", "Russian", "ru-RU", null, null),
            },
            Array.Empty<CatalogLocaleRenameRequest>(),
            new[] { "en" });

        Assert.Null(result.Error);
        CatalogResponse catalog = result.Catalog!;
        Assert.Equal("ru", catalog.DefaultLocale);
        Assert.Single(catalog.Locales);
        CatalogEntryResponse entry = Assert.Single(catalog.Entries);
        Assert.False(entry.Locales.ContainsKey("en"));
        Assert.True(entry.Locales.ContainsKey("ru"));
    }

    [Fact]
    public void ApplyLocales_RenamePropagatesEveryCatalogReference()
    {
        EditorSession session = OpenSession();
        CatalogResponse before = session.GetCatalogSnapshot()!;

        CatalogEditResult result = session.ApplyLocales(
            before.Revision,
            "en-US",
            new[]
            {
                new CatalogLocaleEditRequest("en-US", "English", "en-US", null, null),
                new CatalogLocaleEditRequest("ru", "Russian", "ru-RU", "en-US", null),
            },
            new[] { new CatalogLocaleRenameRequest("en", "en-US") },
            Array.Empty<string>());

        Assert.Null(result.Error);
        CatalogResponse catalog = result.Catalog!;
        Assert.Equal("en-US", catalog.DefaultLocale);
        Assert.Equal("en-US", Assert.Single(catalog.Locales, locale => locale.Id == "ru").Fallback);
        CatalogEntryResponse entry = Assert.Single(catalog.Entries);
        Assert.True(entry.Locales.ContainsKey("en-US"));
        Assert.False(entry.Locales.ContainsKey("en"));
        Assert.Equal("Hello", entry.Locales["en-US"].Text);

        CatalogEditResult undoResult = session.ApplyLocales(
            catalog.Revision,
            "en",
            new[]
            {
                new CatalogLocaleEditRequest("en", "English", "en-US", null, null),
                new CatalogLocaleEditRequest("ru", "Russian", "ru-RU", "en", null),
            },
            new[] { new CatalogLocaleRenameRequest("en-US", "en") },
            Array.Empty<string>());

        Assert.Null(undoResult.Error);
        CatalogResponse restored = undoResult.Catalog!;
        Assert.Equal("en", restored.DefaultLocale);
        Assert.Equal("en", Assert.Single(restored.Locales, locale => locale.Id == "ru").Fallback);
        CatalogEntryResponse restoredEntry = Assert.Single(restored.Entries);
        Assert.True(restoredEntry.Locales.ContainsKey("en"));
        Assert.False(restoredEntry.Locales.ContainsKey("en-US"));
    }

    private static EditorSession OpenSession()
    {
        var catalog = new I18nCatalog
        {
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new() { Id = "en", DisplayName = "English", Culture = "en-US" },
                new() { Id = "ru", DisplayName = "Russian", Culture = "ru-RU", Fallback = "en" },
            },
            Entries = new List<I18nEntry>
            {
                new()
                {
                    Id = "123",
                    Path = "Example.Entry",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new() { Text = "Hello" },
                        ["ru"] = new() { Text = "Привет" },
                    },
                },
            },
        };

        var session = new EditorSession();
        session.Open("C:\\catalog.json", catalog, "CONTENT_HASH");
        return session;
    }
}
