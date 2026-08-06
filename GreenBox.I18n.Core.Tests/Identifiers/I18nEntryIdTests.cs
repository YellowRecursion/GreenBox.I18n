using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nEntryIdTests
{
    private const long ValidId = 3857333080842830204;

    [Fact]
    public void Generate_RepeatedCalls_ReturnValidUniqueIds()
    {
        var ids = new HashSet<long>();

        for (int index = 0; index < 100; index++)
        {
            long id = I18nEntryId.Generate();

            Assert.True(I18nEntryId.IsValid(id));
            Assert.True(ids.Add(id));
        }
    }

    [Fact]
    public void IsValid_CurrentFormat_ReturnsTrue()
    {
        Assert.True(I18nEntryId.IsValid(ValidId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3857333080842830205)]
    public void IsValid_InvalidValue_ReturnsFalse(long value)
    {
        Assert.False(I18nEntryId.IsValid(value));
    }

    [Theory]
    [InlineData("3857333080842830204", true)]
    [InlineData(" 3857333080842830204", false)]
    [InlineData("3857333080842830204 ", false)]
    [InlineData("1", false)]
    [InlineData("not-an-id", false)]
    [InlineData(null, false)]
    public void TryParse_Value_ReturnsExpectedResult(string? value, bool expected)
    {
        bool result = I18nEntryId.TryParse(value, out long id);

        Assert.Equal(expected, result);
        if (expected)
        {
            Assert.Equal(ValidId, id);
        }
    }
}
