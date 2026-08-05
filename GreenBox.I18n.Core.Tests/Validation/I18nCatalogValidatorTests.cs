using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogValidatorTests
{
    [Fact]
    public void Validate_NextIdAfterAllEntries_IsValid()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.NextId = "2";

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == I18nValidationCodes.InvalidNextId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("invalid")]
    public void Validate_InvalidNextId_ReturnsStableError(string nextId)
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.NextId = nextId;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == I18nValidationCodes.InvalidNextId);
        Assert.Equal("$.nextId", diagnostic.JsonPath);
        Assert.Equal(I18nValidationSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Validate_ValidCatalog_ReturnsValidResult()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_NullCatalog_ReturnsNullCatalogError()
    {
        I18nValidationResult result = I18nCatalogValidator.Validate(null);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.True(result.HasErrors);
        Assert.Equal(I18nValidationCodes.NullCatalog, diagnostic.Code);
        Assert.Equal(I18nValidationSeverity.Error, diagnostic.Severity);
        Assert.Equal("$", diagnostic.JsonPath);
        Assert.Null(diagnostic.Target.EntryId);
        Assert.Null(diagnostic.Target.EntryPath);
        Assert.Null(diagnostic.Target.LocaleId);
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.SchemaVersion = I18nCatalogValidator.CurrentSchemaVersion + 1;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.True(result.HasErrors);
        Assert.True(result.Contains(I18nValidationCodes.UnsupportedSchemaVersion));
    }

    [Fact]
    public void Validate_NullEntries_ReturnsNullEntriesError()
    {
        I18nCatalog catalog = CreateCatalog();
        catalog.Entries = null!;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullEntries, diagnostic.Code);
        Assert.Equal("$.entries", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_MissingDefaultLocale_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.DefaultLocale = "";

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.MissingDefaultLocale, diagnostic.Code);
        Assert.Equal("$.defaultLocale", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_UnknownDefaultLocale_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.DefaultLocale = "ru";

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.UnknownDefaultLocale, diagnostic.Code);
    }

    [Fact]
    public void Validate_NullLocaleDefinitions_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales = null!;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullLocaleDefinitions, diagnostic.Code);
        Assert.Equal("$.locales", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_EmptyLocaleDefinitions_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Clear();

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.MissingLocaleDefinitions, diagnostic.Code);
    }

    [Fact]
    public void Validate_NullLocaleDefinition_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(null!);

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullLocaleDefinition, diagnostic.Code);
        Assert.Equal("$.locales[1]", diagnostic.JsonPath);
    }

    [Theory]
    [InlineData(null, I18nValidationCodes.MissingLocaleId)]
    [InlineData("", I18nValidationCodes.MissingLocaleId)]
    [InlineData("_ru", I18nValidationCodes.InvalidLocaleId)]
    [InlineData("ru--RU", I18nValidationCodes.InvalidLocaleId)]
    public void Validate_InvalidLocaleDefinitionId_ReturnsExpectedError(string? localeId, string expectedCode)
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = localeId!,
            DisplayName = "Test locale",
            Culture = "ru-RU",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("$.locales[1].id", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_DuplicateLocaleIdIgnoringCase_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "EN",
            DisplayName = "English duplicate",
            Culture = "en-US",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.DuplicateLocaleId, diagnostic.Code);
        Assert.Equal("$.locales[1].id", diagnostic.JsonPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingLocaleDisplayName_ReturnsError(string? displayName)
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales[0].DisplayName = displayName!;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.MissingLocaleDisplayName, diagnostic.Code);
        Assert.Equal("$.locales[0].displayName", diagnostic.JsonPath);
        Assert.Null(diagnostic.Target.EntryId);
        Assert.Null(diagnostic.Target.EntryPath);
        Assert.Equal("en", diagnostic.Target.LocaleId);
    }

    [Theory]
    [InlineData(null, I18nValidationCodes.MissingLocaleCulture)]
    [InlineData("", I18nValidationCodes.MissingLocaleCulture)]
    [InlineData("invalid culture!", I18nValidationCodes.InvalidLocaleCulture)]
    public void Validate_InvalidLocaleCulture_ReturnsExpectedError(string? culture, string expectedCode)
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru",
            DisplayName = "Русский",
            Culture = culture!,
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("$.locales[1].culture", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_UnknownFallbackLocale_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru",
            DisplayName = "Русский",
            Culture = "ru-RU",
            Fallback = "unknown",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.UnknownFallbackLocale, diagnostic.Code);
        Assert.Equal("$.locales[1].fallback", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_DefaultLocaleWithFallback_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales[0].Fallback = "ru";
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru",
            DisplayName = "Русский",
            Culture = "ru-RU",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.DefaultLocaleHasFallback, diagnostic.Code);
        Assert.Equal("$.locales[0].fallback", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_LocaleFallbackCycle_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "fr",
            DisplayName = "Français",
            Culture = "fr-FR",
            Fallback = "de",
        });
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "de",
            DisplayName = "Deutsch",
            Culture = "de-DE",
            Fallback = "fr",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.LocaleFallbackCycle, diagnostic.Code);
    }

    [Fact]
    public void Validate_UndeclaredEntryLocale_ReturnsError()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["ru"] = new I18nLocaleValue { Text = "Текст" };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.UndeclaredLocale, diagnostic.Code);
        Assert.Equal("$.entries[0].locales['ru']", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_MissingDeclaredLocaleValue_ReturnsWarning()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru",
            DisplayName = "Русский",
            Culture = "ru-RU",
            Fallback = "en",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.HasErrors);
        Assert.Equal(I18nValidationCodes.MissingLocaleValue, diagnostic.Code);
        Assert.Equal(I18nValidationSeverity.Warning, diagnostic.Severity);
        Assert.Equal("$.entries[0].locales['ru']", diagnostic.JsonPath);
        Assert.Equal("1", diagnostic.Target.EntryId);
        Assert.Equal("Reports.ContextMenu.ReportNicknameButton", diagnostic.Target.EntryPath);
        Assert.Equal("ru", diagnostic.Target.LocaleId);
    }

    [Fact]
    public void Validate_ValidLocaleFallback_ReturnsValidResult()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["ru"] = new I18nLocaleValue { Text = "Текст" };
        I18nCatalog catalog = CreateCatalog(entry);
        catalog.Locales.Add(new I18nLocaleDefinition
        {
            Id = "ru",
            DisplayName = "Русский",
            Culture = "ru-RU",
            Fallback = "en",
        });

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_NullEntry_ReturnsNullEntryError()
    {
        I18nCatalog catalog = CreateCatalog();
        catalog.Entries.Add(null!);

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullEntry, diagnostic.Code);
        Assert.Equal("$.entries[0]", diagnostic.JsonPath);
    }

    [Theory]
    [InlineData(null, I18nValidationCodes.MissingId)]
    [InlineData("", I18nValidationCodes.MissingId)]
    [InlineData("   ", I18nValidationCodes.MissingId)]
    [InlineData("0", I18nValidationCodes.InvalidId)]
    [InlineData("-1", I18nValidationCodes.InvalidId)]
    [InlineData("1.5", I18nValidationCodes.InvalidId)]
    [InlineData(" 1", I18nValidationCodes.InvalidId)]
    public void Validate_InvalidId_ReturnsExpectedError(string? id, string expectedCode)
    {
        I18nEntry entry = CreateValidEntry();
        entry.Id = id!;

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("$.entries[0].id", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_DuplicateId_ReturnsErrorForSecondEntry()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateValidEntry("1", "Reports.First"),
            CreateValidEntry("1", "Reports.Second"));

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.DuplicateId, diagnostic.Code);
        Assert.Equal("$.entries[1].id", diagnostic.JsonPath);
    }

    [Theory]
    [InlineData(null, I18nValidationCodes.MissingPath)]
    [InlineData("", I18nValidationCodes.MissingPath)]
    [InlineData("   ", I18nValidationCodes.MissingPath)]
    [InlineData(".Reports.Button", I18nValidationCodes.InvalidPath)]
    [InlineData("Reports..Button", I18nValidationCodes.InvalidPath)]
    [InlineData("Reports.Context-Menu.Button", I18nValidationCodes.InvalidPath)]
    [InlineData("Reports.Button ", I18nValidationCodes.InvalidPath)]
    public void Validate_InvalidPath_ReturnsExpectedError(string? path, string expectedCode)
    {
        I18nEntry entry = CreateValidEntry();
        entry.Path = path!;

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("$.entries[0].path", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_PathDifferingOnlyByCase_ReturnsDuplicatePathError()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateValidEntry("1", "Reports.ContextMenu.Button"),
            CreateValidEntry("2", "reports.contextmenu.button"));

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.DuplicatePath, diagnostic.Code);
        Assert.Equal("$.entries[1].path", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_MissingLocales_ReturnsNonBlockingWarning()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales.Clear();

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(I18nValidationCodes.MissingLocales, diagnostic.Code);
        Assert.Equal(I18nValidationSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Validate_EmptyLocaleValue_ReturnsNonBlockingWarning()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["en"] = new I18nLocaleValue();

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.HasErrors);
        Assert.Equal(I18nValidationCodes.EmptyLocaleValue, diagnostic.Code);
        Assert.Equal("$.entries[0].locales['en']", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_EmptyLocaleId_ReturnsError()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales.Clear();
        entry.Locales[""] = new I18nLocaleValue { Text = "Text" };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.InvalidLocaleId, diagnostic.Code);
        Assert.Equal("$.entries[0].locales", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_NullLocaleValue_ReturnsError()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["en"] = null!;

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullLocaleValue, diagnostic.Code);
        Assert.Equal("$.entries[0].locales['en']", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_InvalidLocaleIcon_ReturnsAssetDiagnosticAtLocalePath()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.Locales[0].Icon = new I18nAssetReference
        {
            AssetGuid = "invalid",
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.InvalidAssetGuid, diagnostic.Code);
        Assert.Equal("$.locales[0].icon.assetGuid", diagnostic.JsonPath);
    }

    [Theory]
    [InlineData(null, I18nValidationCodes.MissingAssetGuid)]
    [InlineData("", I18nValidationCodes.MissingAssetGuid)]
    [InlineData("not-a-guid", I18nValidationCodes.InvalidAssetGuid)]
    [InlineData("0123456789abcdef0123456789abcdeg", I18nValidationCodes.InvalidAssetGuid)]
    public void Validate_InvalidAssetGuid_ReturnsExpectedError(string? assetGuid, string expectedCode)
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["en"].Asset = new I18nAssetReference
        {
            AssetGuid = assetGuid!,
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal("$.entries[0].locales['en'].asset.assetGuid", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_ValidAssetGuid_DoesNotReturnDiagnostic()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["en"].Asset = new I18nAssetReference
        {
            AssetGuid = "0123456789abcdefABCDEF0123456789",
            LocalFileId = "21300000",
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    public void Validate_InvalidAssetLocalFileId_ReturnsError(string localFileId)
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales["en"].Asset = new I18nAssetReference
        {
            AssetGuid = "0123456789abcdef0123456789abcdef",
            LocalFileId = localFileId,
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.InvalidAssetLocalFileId, diagnostic.Code);
        Assert.Equal("$.entries[0].locales['en'].asset.localFileId", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_MultipleProblems_ReturnsAllDiagnosticsInRuleOrder()
    {
        var entry = new I18nEntry
        {
            Id = "",
            Path = "",
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        Assert.Equal(2, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(
            new[]
            {
                I18nValidationCodes.MissingId,
                I18nValidationCodes.MissingPath,
                I18nValidationCodes.MissingLocales,
            },
            result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void Validate_Locales_ReturnsDiagnosticsInOrdinalLocaleOrder()
    {
        I18nEntry entry = CreateValidEntry();
        entry.Locales.Clear();
        entry.Locales["z"] = CreateLocaleWithInvalidAsset();
        entry.Locales["a"] = CreateLocaleWithInvalidAsset();
        I18nCatalog catalog = CreateCatalog(entry);
        catalog.DefaultLocale = "a";
        catalog.Locales = new List<I18nLocaleDefinition>
        {
            new() { Id = "z", DisplayName = "Zulu", Culture = "en-US" },
            new() { Id = "a", DisplayName = "Alpha", Culture = "en-US" },
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.Equal(
            new[]
            {
                "$.entries[0].locales['a'].asset.assetGuid",
                "$.entries[0].locales['z'].asset.assetGuid",
            },
            result.Diagnostics.Select(diagnostic => diagnostic.JsonPath));
    }

    [Fact]
    public void Validate_UnsortedEntries_ReturnsEntriesNotSortedError()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateValidEntry("10", "Units.Tank10.Title"),
            CreateValidEntry("2", "Units.Tank2.Title"));

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.EntriesNotSorted, diagnostic.Code);
        Assert.Equal(I18nValidationSeverity.Error, diagnostic.Severity);
        Assert.Equal("$.entries[1]", diagnostic.JsonPath);
        Assert.Equal("2", diagnostic.Target.EntryId);
        Assert.Equal("Units.Tank2.Title", diagnostic.Target.EntryPath);
        Assert.Null(diagnostic.Target.LocaleId);
        Assert.Contains("'Units.Tank2.Title' must appear before 'Units.Tank10.Title'", diagnostic.Message);
    }

    [Fact]
    public void Validate_NaturallySortedEntries_ReturnsValidResult()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateValidEntry("1", "Units.Tank2.Title"),
            CreateValidEntry("2", "Units.Tank02.Title"),
            CreateValidEntry("10", "Units.Tank10.Title"));

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Diagnostics);
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        return new I18nCatalog
        {
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new()
                {
                    Id = "en",
                    DisplayName = "English",
                    Culture = "en-US",
                },
            },
            Entries = new List<I18nEntry>(entries),
        };
    }

    private static I18nEntry CreateValidEntry(
        string id = "1",
        string path = "Reports.ContextMenu.ReportNicknameButton")
    {
        return new I18nEntry
        {
            Id = id,
            Path = path,
            Locales = new Dictionary<string, I18nLocaleValue>
            {
                ["en"] = new I18nLocaleValue
                {
                    Text = "Report nickname",
                },
            },
        };
    }

    private static I18nLocaleValue CreateLocaleWithInvalidAsset()
    {
        return new I18nLocaleValue
        {
            Text = "Text",
            Asset = new I18nAssetReference
            {
                AssetGuid = "invalid",
            },
        };
    }
}
