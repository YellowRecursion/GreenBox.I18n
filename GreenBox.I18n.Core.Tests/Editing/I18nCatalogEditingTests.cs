using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogEditingTests
{
    [Fact]
    public void MoveEntry_ValidPath_ChangesOnlyPathAndRestoresCanonicalOrder()
    {
        I18nEntry movedEntry = CreateEntry("2", "Units.Tank10.Title");
        I18nEntry otherEntry = CreateEntry("1", "Units.Tank2.Title");
        I18nCatalog catalog = CreateCatalog(otherEntry, movedEntry);
        Dictionary<string, I18nLocaleValue> originalLocales = movedEntry.Locales;

        I18nEditResult result = catalog.MoveEntry(2, "Units.Tank1.Title");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Same(movedEntry, result.Entry);
        Assert.Null(result.Error);
        Assert.Equal("2", movedEntry.Id);
        Assert.Equal("Units.Tank1.Title", movedEntry.Path);
        Assert.Same(originalLocales, movedEntry.Locales);
        Assert.Equal(new[] { movedEntry, otherEntry }, catalog.Entries);
    }

    [Fact]
    public void MoveEntry_SamePath_ReturnsSuccessWithoutChanges()
    {
        I18nEntry entry = CreateEntry("1", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(1, "Reports.Title");

        Assert.True(result.IsSuccess);
        Assert.False(result.HasChanges);
        Assert.Same(entry, result.Entry);
    }

    [Fact]
    public void MoveEntry_PathCaseChanged_UpdatesPath()
    {
        I18nEntry entry = CreateEntry("1", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(1, "Reports.title");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Equal("Reports.title", entry.Path);
    }

    [Fact]
    public void MoveEntry_NonPositiveId_ReturnsInvalidIdWithoutChanges()
    {
        I18nEntry entry = CreateEntry("1", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(0, "Reports.NewTitle");

        AssertFailure(result, I18nEditCodes.InvalidId);
        Assert.Equal("Reports.Title", entry.Path);
    }

    [Fact]
    public void MoveEntry_MissingEntry_ReturnsEntryNotFoundWithoutChanges()
    {
        I18nEntry entry = CreateEntry("1", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(2, "Reports.NewTitle");

        AssertFailure(result, I18nEditCodes.EntryNotFound);
        Assert.Equal("Reports.Title", entry.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Reports..Title")]
    [InlineData("Reports.Context-Menu.Title")]
    public void MoveEntry_InvalidPath_ReturnsInvalidPathWithoutChanges(string? newPath)
    {
        I18nEntry entry = CreateEntry("1", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(1, newPath);

        AssertFailure(result, I18nEditCodes.InvalidPath);
        Assert.Equal("Reports.Title", entry.Path);
    }

    [Fact]
    public void MoveEntry_DuplicatePathIgnoringCase_ReturnsDuplicatePathWithoutChanges()
    {
        I18nEntry first = CreateEntry("1", "Reports.Subtitle");
        I18nEntry second = CreateEntry("2", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(first, second);

        I18nEditResult result = catalog.MoveEntry(1, "reports.title");

        AssertFailure(result, I18nEditCodes.DuplicatePath);
        Assert.Equal("Reports.Subtitle", first.Path);
    }

    private static void AssertFailure(I18nEditResult result, string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.False(result.HasChanges);
        Assert.Null(result.Entry);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        return new I18nCatalog
        {
            Entries = entries.ToList(),
        };
    }

    private static I18nEntry CreateEntry(string id, string path)
    {
        return new I18nEntry
        {
            Id = id,
            Path = path,
            Locales = new Dictionary<string, I18nLocaleValue>
            {
                ["en"] = new I18nLocaleValue
                {
                    Text = path,
                },
            },
        };
    }
}
