using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogValidatorTests
{
    [Fact]
    public void Validate_ValidCatalog_ReturnsValidResult()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_NullCatalog_ReturnsNullCatalogError()
    {
        I18nValidationResult result = I18nCatalogValidator.Validate(null);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsValid);
        Assert.Equal(I18nValidationCodes.NullCatalog, diagnostic.Code);
        Assert.Equal(I18nValidationSeverity.Error, diagnostic.Severity);
        Assert.Equal("$", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_ReturnsError()
    {
        I18nCatalog catalog = CreateCatalog(CreateValidEntry());
        catalog.SchemaVersion = I18nCatalogValidator.CurrentSchemaVersion + 1;

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        Assert.False(result.IsValid);
        Assert.True(result.Contains(I18nValidationCodes.UnsupportedSchemaVersion));
    }

    [Fact]
    public void Validate_NullEntries_ReturnsNullEntriesError()
    {
        var catalog = new I18nCatalog
        {
            Entries = null!,
        };

        I18nValidationResult result = I18nCatalogValidator.Validate(catalog);

        I18nValidationDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(I18nValidationCodes.NullEntries, diagnostic.Code);
        Assert.Equal("$.entries", diagnostic.JsonPath);
    }

    [Fact]
    public void Validate_NullEntry_ReturnsNullEntryError()
    {
        var catalog = new I18nCatalog();
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
        Assert.True(result.IsValid);
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
        Assert.True(result.IsValid);
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

        Assert.True(result.IsValid);
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

        I18nValidationResult result = I18nCatalogValidator.Validate(CreateCatalog(entry));

        Assert.Equal(
            new[]
            {
                "$.entries[0].locales['a'].asset.assetGuid",
                "$.entries[0].locales['z'].asset.assetGuid",
            },
            result.Diagnostics.Select(diagnostic => diagnostic.JsonPath));
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        return new I18nCatalog
        {
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
