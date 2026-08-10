using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nRuntimeTests
{
    [Fact]
    public void Constructor_DefaultLocale_BuildsLocaleSnapshot()
    {
        I18nCatalog catalog = CreateCatalog();

        var runtime = new I18nRuntime(catalog);

        Assert.Equal("en", runtime.CurrentLocale.Id);
        Assert.Same(runtime.DefaultLocale, runtime.CurrentLocale);
        Assert.Equal("English", runtime.CurrentLocale.DisplayName);
        Assert.Equal("en-US", runtime.CurrentCulture.Name);
        Assert.Equal(new[] { "en", "ru" }, runtime.Locales.Select(locale => locale.Id));
    }

    [Fact]
    public void Constructor_RequestedLocale_SelectsLocale()
    {
        var runtime = new I18nRuntime(CreateCatalog(), "ru");

        Assert.Equal("ru", runtime.CurrentLocale.Id);
        Assert.Equal("ru-RU", runtime.CurrentCulture.Name);
    }

    [Fact]
    public void Constructor_InvalidCatalog_ThrowsWithValidationResult()
    {
        I18nCatalog catalog = CreateCatalog();
        catalog.SchemaVersion = 2;

        I18nInvalidCatalogException exception = Assert.Throws<I18nInvalidCatalogException>(
            () => new I18nRuntime(catalog));

        Assert.True(exception.ValidationResult.HasErrors);
        Assert.True(exception.ValidationResult.Contains(I18nValidationCodes.UnsupportedSchemaVersion));
    }

    [Fact]
    public void Constructor_UnknownInitialLocale_Throws()
    {
        Assert.Throws<ArgumentException>(() => new I18nRuntime(CreateCatalog(), "de"));
    }

    [Fact]
    public void Text_CurrentLocaleContainsText_ReturnsCurrentText()
    {
        I18nEntry entry = CreateEntry(
            "3857333080842830204",
            "Menu.Play",
            ("en", "Play", null),
            ("ru", "Играть", null));
        var runtime = new I18nRuntime(CreateCatalog(entry), "ru");

        string result = runtime.Text(3857333080842830204);

        Assert.Equal("Играть", result);
    }

    [Fact]
    public void Text_ExplicitFallbackChain_ReturnsNearestText()
    {
        I18nEntry entry = CreateEntry(
            "3857333080842830204",
            "Menu.Play",
            ("en", "Play", null),
            ("ru", "Играть", null));
        I18nCatalog catalog = CreateCatalog(entry);
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru-RU",
            DisplayName = "Русский (Россия)",
            Culture = "ru-RU",
            Fallback = "ru",
        });
        var runtime = new I18nRuntime(catalog, "ru-RU");

        string result = runtime.Text(3857333080842830204);

        Assert.Equal("Играть", result);
    }

    [Fact]
    public void Text_NoExplicitFallback_ReturnsDefaultLocaleText()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Menu.Play", ("en", "Play", null));
        var runtime = new I18nRuntime(CreateCatalog(entry), "ru");

        string result = runtime.Text(3857333080842830204);

        Assert.Equal("Play", result);
    }

    [Fact]
    public void Text_EmptyCurrentText_DoesNotFallBack()
    {
        I18nEntry entry = CreateEntry(
            "3857333080842830204",
            "Menu.Play",
            ("en", "Play", null),
            ("ru", string.Empty, null));
        var runtime = new I18nRuntime(CreateCatalog(entry), "ru");

        bool found = runtime.TryGetText(3857333080842830204, out string? result);

        Assert.True(found);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Text_NoText_ReturnsPath()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Menu.Play", ("en", null, CreateAsset("a")));
        var runtime = new I18nRuntime(CreateCatalog(entry));

        string result = runtime.Text(3857333080842830204);

        Assert.Equal("Menu.Play", result);
    }

    [Fact]
    public void Text_UnknownId_ReturnsNumericId()
    {
        var runtime = new I18nRuntime(CreateCatalog());

        string result = runtime.Text(3857333080842830951);

        Assert.Equal("3857333080842830951", result);
    }

    [Fact]
    public void Text_NonPositiveId_Throws()
    {
        var runtime = new I18nRuntime(CreateCatalog());

        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Text(0));
    }

    [Fact]
    public void TextAndAsset_ResolveFallbackIndependently()
    {
        I18nAssetReference englishAsset = CreateAsset("a");
        I18nEntry entry = CreateEntry(
            "3857333080842830204",
            "Narrative.Greeting",
            ("en", "Hello", englishAsset),
            ("ru", "Привет", null));
        var runtime = new I18nRuntime(CreateCatalog(entry), "ru");

        string text = runtime.Text(3857333080842830204);
        I18nAssetReference? asset = runtime.Asset(3857333080842830204);

        Assert.Equal("Привет", text);
        Assert.NotNull(asset);
        Assert.Equal(englishAsset.AssetGuid, asset.AssetGuid);
    }

    [Fact]
    public void TryGetAsset_MissingAsset_ReturnsFalse()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Menu.Play", ("en", "Play", null));
        var runtime = new I18nRuntime(CreateCatalog(entry));

        bool found = runtime.TryGetAsset(3857333080842830204, out I18nAssetReference? asset);

        Assert.False(found);
        Assert.Null(asset);
    }

    [Fact]
    public void SetLocale_DifferentLocale_RaisesEventAfterChange()
    {
        var runtime = new I18nRuntime(CreateCatalog());
        I18nLocaleChangedEventArgs? receivedArgs = null;
        string? localeObservedByHandler = null;
        runtime.LocaleChanged += (_, args) =>
        {
            receivedArgs = args;
            localeObservedByHandler = runtime.CurrentLocale.Id;
        };

        bool changed = runtime.SetLocale("ru");

        Assert.True(changed);
        Assert.NotNull(receivedArgs);
        Assert.Equal("en", receivedArgs.PreviousLocale.Id);
        Assert.Equal("ru", receivedArgs.CurrentLocale.Id);
        Assert.Equal("ru", localeObservedByHandler);
    }

    [Fact]
    public void SetLocale_CurrentLocale_DoesNotRaiseEvent()
    {
        var runtime = new I18nRuntime(CreateCatalog());
        int eventCount = 0;
        runtime.LocaleChanged += (_, _) => eventCount++;

        bool changed = runtime.SetLocale("en");

        Assert.False(changed);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void SetLocale_UnknownLocale_ThrowsWithoutChangingLocale()
    {
        var runtime = new I18nRuntime(CreateCatalog());

        Assert.Throws<ArgumentException>(() => runtime.SetLocale("de"));
        Assert.Equal("en", runtime.CurrentLocale.Id);
    }

    [Fact]
    public void Constructor_CatalogChangesAfterCreation_DoNotChangeRuntimeSnapshot()
    {
        I18nAssetReference sourceAsset = CreateAsset("a");
        I18nEntry sourceEntry = CreateEntry("3857333080842830204", "Menu.Play", ("en", "Play", sourceAsset));
        I18nCatalog catalog = CreateCatalog(sourceEntry);
        var runtime = new I18nRuntime(catalog);

        sourceEntry.Path = "Menu.Changed";
        sourceEntry.Locales["en"].Text = "Changed";
        sourceAsset.AssetGuid = new string('b', 32);
        catalog.Locales[0].DisplayName = "Changed";

        Assert.Equal("Play", runtime.Text(3857333080842830204));
        Assert.Equal(new string('a', 32), runtime.Asset(3857333080842830204)?.AssetGuid);
        Assert.Equal("English", runtime.CurrentLocale.DisplayName);
    }

    [Fact]
    public void LocaleIcon_ReturnedReferenceCannotMutateRuntimeSnapshot()
    {
        I18nCatalog catalog = CreateCatalog();
        catalog.Locales[0].Icon = CreateAsset("a");
        var runtime = new I18nRuntime(catalog);

        I18nAssetReference? returnedIcon = runtime.CurrentLocale.Icon;
        Assert.NotNull(returnedIcon);
        returnedIcon.AssetGuid = new string('b', 32);

        Assert.Equal(new string('a', 32), runtime.CurrentLocale.Icon?.AssetGuid);
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        var catalog = new I18nCatalog
        {
            DefaultLocale = "en",
            Locales =
            {
                new I18nLocaleDefinition
                {
                    Id = "en",
                    DisplayName = "English",
                    Culture = "en-US",
                },
                new I18nLocaleDefinition
                {
                    Id = "ru",
                    DisplayName = "Русский",
                    Culture = "ru-RU",
                    Fallback = "en",
                },
            },
            Entries = entries.ToList(),
        };

        catalog.Entries.Sort(I18nEntryComparer.Canonical);
        return catalog;
    }

    private static I18nEntry CreateEntry(
        string id,
        string path,
        params (string LocaleId, string? Text, I18nAssetReference? Asset)[] values)
    {
        var entry = new I18nEntry
        {
            Id = id,
            Path = path,
        };

        foreach ((string localeId, string? text, I18nAssetReference? asset) in values)
        {
            entry.Locales.Add(localeId, new I18nLocaleValue
            {
                Text = text,
                Asset = asset,
            });
        }

        return entry;
    }

    private static I18nAssetReference CreateAsset(string hexadecimalDigit)
    {
        return new I18nAssetReference
        {
            AssetGuid = string.Concat(Enumerable.Repeat(hexadecimalDigit, 32)),
        };
    }
}
