using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogQueriesTests
{
    [Fact]
    public void FindById_ExistingId_ReturnsEntry()
    {
        I18nEntry expected = CreateEntry("42", "Reports.Title");
        I18nCatalog catalog = CreateCatalog(CreateEntry("1", "Menu.Title"), expected);

        I18nEntry? result = catalog.FindById(42);

        Assert.Same(expected, result);
    }

    [Fact]
    public void FindById_MissingId_ReturnsNull()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("1", "Reports.Title"));

        I18nEntry? result = catalog.FindById(2);

        Assert.Null(result);
    }

    [Fact]
    public void FindById_NonPositiveId_Throws()
    {
        var catalog = new I18nCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.FindById(0));
    }

    [Fact]
    public void Search_MatchesPathCommentAndLocalizedTextIgnoringCase()
    {
        I18nEntry pathMatch = CreateEntry("1", "Reports.Nickname");
        I18nEntry commentMatch = CreateEntry("2", "Profile.Name", "Shown in REPORTS");
        I18nEntry textMatch = CreateEntry("3", "Menu.Title", text: "Open reports");
        I18nCatalog catalog = CreateCatalog(pathMatch, commentMatch, textMatch);

        IReadOnlyList<I18nEntry> result = catalog.Search("reports");

        Assert.Equal(new[] { pathMatch, textMatch, commentMatch }, result);
    }

    [Fact]
    public void Search_RanksPathMatchesAndUsesDeterministicTieBreakers()
    {
        I18nEntry content = CreateEntry("1", "Menu.Title", text: "Reports");
        I18nEntry contains = CreateEntry("2", "Main.Reports.Title");
        I18nEntry prefixSecond = CreateEntry("10", "Reports.Zulu");
        I18nEntry prefixFirst = CreateEntry("9", "Reports.Alpha");
        I18nEntry exact = CreateEntry("3", "Reports");
        I18nCatalog catalog = CreateCatalog(content, prefixSecond, contains, exact, prefixFirst);

        IReadOnlyList<I18nEntry> result = catalog.Search("reports");

        Assert.Equal(new[] { exact, prefixFirst, prefixSecond, contains, content }, result);
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmptyList()
    {
        I18nCatalog catalog = CreateCatalog(CreateEntry("1", "Reports.Title"));

        IReadOnlyList<I18nEntry> result = catalog.Search("Settings");

        Assert.Empty(result);
    }

    [Fact]
    public void Search_EqualRanks_UsesCanonicalNaturalOrder()
    {
        I18nEntry tank10 = CreateEntry("10", "Units.Tank10.Title");
        I18nEntry tank2 = CreateEntry("2", "Units.Tank2.Title");
        I18nCatalog catalog = CreateCatalog(tank10, tank2);

        IReadOnlyList<I18nEntry> result = catalog.Search("Units");

        Assert.Equal(new[] { tank2, tank10 }, result);
    }

    [Fact]
    public void Search_EmptyQuery_Throws()
    {
        var catalog = new I18nCatalog();

        Assert.Throws<ArgumentException>(() => catalog.Search("  "));
    }

    private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
    {
        return new I18nCatalog
        {
            Entries = entries.ToList(),
        };
    }

    private static I18nEntry CreateEntry(
        string id,
        string path,
        string? comment = null,
        string? text = null)
    {
        var entry = new I18nEntry
        {
            Id = id,
            Path = path,
            Comment = comment,
        };

        if (text != null)
        {
            entry.Locales["en"] = new I18nLocaleValue
            {
                Text = text,
            };
        }

        return entry;
    }
}
