using Newtonsoft.Json;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogSerializationTests
{
    [Fact]
    public void SerializeEntry_WithoutComment_OmitsCommentProperty()
    {
        var entry = new I18nEntry
        {
            Id = "3857333080842830204",
            Path = "Reports.ContextMenu.ReportNicknameButton",
        };

        string json = JsonConvert.SerializeObject(entry);

        Assert.DoesNotContain("\"comment\"", json);
    }

    [Fact]
    public void SerializeLocaleValue_WithoutTextOrAsset_OmitsBothProperties()
    {
        string json = JsonConvert.SerializeObject(new I18nLocaleValue());

        Assert.Equal("{}", json);
    }
}
