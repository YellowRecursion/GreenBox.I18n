using GreenBox.I18n;
using Newtonsoft.Json;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogJsonTests
{
    [Fact]
    public void Serialize_ReturnsCanonicalFormattedJson()
    {
        var catalog = new I18nCatalog
        {
            Entries = new List<I18nEntry>
            {
                new()
                {
                    Id = "1",
                    Path = "Reports.ContextMenu.ReportNicknameButton",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["ru"] = new() { Text = "Пожаловаться" },
                        ["en"] = new()
                        {
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "0123456789abcdef0123456789abcdef",
                            },
                        },
                    },
                },
            },
        };

        string json = I18nCatalogJson.Serialize(catalog);

        const string expected = """
                                {
                                  "schemaVersion": 1,
                                  "entries": [
                                    {
                                      "id": "1",
                                      "path": "Reports.ContextMenu.ReportNicknameButton",
                                      "locales": {
                                        "en": {
                                          "asset": {
                                            "assetGuid": "0123456789abcdef0123456789abcdef"
                                          }
                                        },
                                        "ru": {
                                          "text": "Пожаловаться"
                                        }
                                      }
                                    }
                                  ]
                                }
                                """;

        Assert.Equal(expected + "\n", json);
        Assert.DoesNotContain('\r', json);
        Assert.DoesNotContain("\"comment\"", json);
    }

    [Fact]
    public void Deserialize_CanonicalJson_ReturnsCatalog()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "entries": [
                                {
                                  "id": "42",
                                  "path": "Reports.Title",
                                  "comment": "Reports screen title.",
                                  "locales": {
                                    "en": {
                                      "text": "Reports"
                                    }
                                  }
                                }
                              ]
                            }
                            """;

        I18nCatalog catalog = I18nCatalogJson.Deserialize(json);

        I18nEntry entry = Assert.Single(catalog.Entries);
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal("42", entry.Id);
        Assert.Equal("Reports.Title", entry.Path);
        Assert.Equal("Reports screen title.", entry.Comment);
        Assert.Equal("Reports", entry.Locales["en"].Text);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesCatalogData()
    {
        var source = new I18nCatalog
        {
            Entries = new List<I18nEntry>
            {
                new()
                {
                    Id = "7",
                    Path = "Reports.Subtitle",
                    Comment = "Subtitle context.",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new()
                        {
                            Text = "Subtitle",
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "abcdef0123456789abcdef0123456789",
                            },
                        },
                    },
                },
            },
        };

        I18nCatalog result = I18nCatalogJson.Deserialize(I18nCatalogJson.Serialize(source));

        I18nEntry entry = Assert.Single(result.Entries);
        Assert.Equal("7", entry.Id);
        Assert.Equal("Reports.Subtitle", entry.Path);
        Assert.Equal("Subtitle context.", entry.Comment);
        Assert.Equal("Subtitle", entry.Locales["en"].Text);
        Assert.Equal("abcdef0123456789abcdef0123456789", entry.Locales["en"].Asset!.AssetGuid);
    }

    [Fact]
    public void Deserialize_DuplicateProperty_ThrowsJsonReaderException()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "schemaVersion": 1,
                              "entries": []
                            }
                            """;

        Assert.Throws<JsonReaderException>(() => I18nCatalogJson.Deserialize(json));
    }

    [Fact]
    public void Deserialize_UnknownProperty_ThrowsJsonSerializationException()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "unknown": true,
                              "entries": []
                            }
                            """;

        Assert.Throws<JsonSerializationException>(() => I18nCatalogJson.Deserialize(json));
    }

    [Fact]
    public void Deserialize_NullJsonValue_ThrowsJsonSerializationException()
    {
        Assert.Throws<JsonSerializationException>(() => I18nCatalogJson.Deserialize("null"));
    }

    [Fact]
    public void Deserialize_AdditionalContent_ThrowsJsonReaderException()
    {
        Assert.Throws<JsonReaderException>(() => I18nCatalogJson.Deserialize("{} {}"));
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsErrorWithLineInformation()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "entries": [
                            }
                            """;

        JsonReaderException exception = Assert.Throws<JsonReaderException>(
            () => I18nCatalogJson.Deserialize(json));

        Assert.Equal(4, exception.LineNumber);
        Assert.Equal(0, exception.LinePosition);
    }

    [Fact]
    public void Deserialize_NullString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => I18nCatalogJson.Deserialize(null!));
    }

    [Fact]
    public void Serialize_NullCatalog_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => I18nCatalogJson.Serialize(null!));
    }
}
