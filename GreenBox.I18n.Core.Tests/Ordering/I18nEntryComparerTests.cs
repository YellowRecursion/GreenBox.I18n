using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nEntryComparerTests
{
    [Theory]
    [InlineData("Tank2", "Tank02")]
    [InlineData("Tank02", "Tank10")]
    [InlineData("Tank9", "Tank10")]
    [InlineData("Tank99999999999999999999", "Tank100000000000000000000")]
    [InlineData("Reports.Tank2.Title", "Reports.Tank10.Title")]
    [InlineData("Tank2", "tank2")]
    public void Compare_FirstPathBelongsBeforeSecondPath(string firstPath, string secondPath)
    {
        I18nEntry first = CreateEntry("3857333080842830204", firstPath);
        I18nEntry second = CreateEntry("3857333080842830453", secondPath);

        int result = I18nEntryComparer.Canonical.Compare(first, second);

        Assert.True(result < 0);
    }

    [Fact]
    public void Compare_EqualPaths_UsesNumericIdTieBreaker()
    {
        I18nEntry first = CreateEntry("3857333080842830453", "Reports.Title");
        I18nEntry second = CreateEntry("3857333080842832461", "Reports.Title");

        int result = I18nEntryComparer.Canonical.Compare(first, second);

        Assert.True(result < 0);
    }

    private static I18nEntry CreateEntry(string id, string path)
    {
        return new I18nEntry
        {
            Id = id,
            Path = path,
        };
    }
}
