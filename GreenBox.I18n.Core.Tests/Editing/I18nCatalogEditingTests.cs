using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogEditingTests
{
    [Fact]
    public void AddEntry_ValidPath_AllocatesSelfIdentifyingId()
    {
        I18nCatalog catalog = CreateCatalog(
            CreateEntry("3857333080842832461", "Menu.Title"),
            CreateEntry("3857333080842834967", "Reports.Title"));

        I18nEditResult result = catalog.AddEntry("Menu.PlayButton");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        long id = long.Parse(result.Entry!.Id);
        Assert.True(I18nEntryId.IsValid(id));
        Assert.Equal("Menu.PlayButton", result.Entry.Path);
        Assert.Empty(result.Entry.Locales);
        Assert.Equal(
            new[] { "Menu.PlayButton", "Menu.Title", "Reports.Title" },
            catalog.Entries.Select(entry => entry.Path));
    }

    [Fact]
    public void AddEntry_RepeatedCalls_AllocateUniqueIds()
    {
        I18nCatalog catalog = CreateCatalog();
        var ids = new HashSet<string>();

        for (int entryIndex = 0; entryIndex < 100; entryIndex++)
        {
            I18nEditResult result = catalog.AddEntry($"Generated.Entry{entryIndex}");

            Assert.True(result.IsSuccess);
            Assert.True(ids.Add(result.Entry!.Id));
        }
    }

    [Fact]
    public void AddEntry_DuplicatePathIgnoringCase_ReturnsDuplicatePathWithoutChanges()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842834967", "Reports.Title"));

        I18nEditResult result = catalog.AddEntry("reports.title");

        AssertFailure(result, I18nEditCodes.DuplicatePath);
        Assert.Single(catalog.Entries);
    }

    [Fact]
    public void RemoveEntry_ExistingId_RemovesOnlyRequestedEntry()
    {
        I18nEntry retainedEntry = CreateEntry("3857333080842832461", "Menu.Title");
        I18nEntry removedEntry = CreateEntry("3857333080842834967", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(retainedEntry, removedEntry);

        I18nEditResult result = catalog.RemoveEntry(3857333080842834967);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Same(removedEntry, result.Entry);
        Assert.Equal(new[] { retainedEntry }, catalog.Entries);
    }

    [Fact]
    public void RemoveEntry_MissingEntry_ReturnsEntryNotFoundWithoutChanges()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842834967", "Reports.Title"));

        I18nEditResult result = catalog.RemoveEntry(3857333080842835216);

        AssertFailure(result, I18nEditCodes.EntryNotFound);
        Assert.Single(catalog.Entries);
    }

    [Fact]
    public void MoveEntry_ValidPath_ChangesOnlyPathAndRestoresCanonicalOrder()
    {
        I18nEntry movedEntry = CreateEntry("3857333080842830453", "Units.Tank10.Title");
        I18nEntry otherEntry = CreateEntry("3857333080842830204", "Units.Tank2.Title");
        I18nCatalog catalog = CreateCatalog(otherEntry, movedEntry);
        Dictionary<string, I18nLocaleValue> originalLocales = movedEntry.Locales;

        I18nEditResult result = catalog.MoveEntry(3857333080842830453, "Units.Tank1.Title");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Same(movedEntry, result.Entry);
        Assert.Null(result.Error);
        Assert.Equal("3857333080842830453", movedEntry.Id);
        Assert.Equal("Units.Tank1.Title", movedEntry.Path);
        Assert.Same(originalLocales, movedEntry.Locales);
        Assert.Equal(new[] { movedEntry, otherEntry }, catalog.Entries);
    }

    [Fact]
    public void MoveEntry_SamePath_ReturnsSuccessWithoutChanges()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(3857333080842830204, "Reports.Title");

        Assert.True(result.IsSuccess);
        Assert.False(result.HasChanges);
        Assert.Same(entry, result.Entry);
    }

    [Fact]
    public void MoveEntry_PathCaseChanged_UpdatesPath()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(3857333080842830204, "Reports.title");

        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Equal("Reports.title", entry.Path);
    }

    [Fact]
    public void MoveEntry_NonPositiveId_ReturnsInvalidIdWithoutChanges()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(0, "Reports.NewTitle");

        AssertFailure(result, I18nEditCodes.InvalidId);
        Assert.Equal("Reports.Title", entry.Path);
    }

    [Fact]
    public void MoveEntry_MissingEntry_ReturnsEntryNotFoundWithoutChanges()
    {
        I18nEntry entry = CreateEntry("3857333080842830204", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(3857333080842830453, "Reports.NewTitle");

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
        I18nEntry entry = CreateEntry("3857333080842830204", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(entry);

        I18nEditResult result = catalog.MoveEntry(3857333080842830204, newPath);

        AssertFailure(result, I18nEditCodes.InvalidPath);
        Assert.Equal("Reports.Title", entry.Path);
    }

    [Fact]
    public void MoveEntry_DuplicatePathIgnoringCase_ReturnsDuplicatePathWithoutChanges()
    {
        I18nEntry first = CreateEntry("3857333080842830204", "Reports.Subtitle");
        I18nEntry second = CreateEntry("3857333080842830453", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(first, second);

        I18nEditResult result = catalog.MoveEntry(3857333080842830204, "reports.title");

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
