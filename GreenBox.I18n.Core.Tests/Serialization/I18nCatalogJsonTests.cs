using GreenBox.I18n;
using Newtonsoft.Json;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCatalogJsonTests
{
    [Fact]
    public void Serialize_NextId_WritesItAfterSchemaVersion()
    {
        var catalog = new I18nCatalog
        {
            NextId = "42",
        };

        string json = I18nCatalogJson.Serialize(catalog);

        Assert.Contains("\"schemaVersion\": 1,\n  \"nextId\": \"42\"", json);
    }

    [Fact]
    public void Serialize_ReturnsCanonicalFormattedJson()
    {
        var catalog = new I18nCatalog
        {
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new() { Id = "en", DisplayName = "English", Culture = "en-US" },
                new()
                {
                    Id = "ru",
                    DisplayName = "Русский",
                    Culture = "ru-RU",
                    Fallback = "en",
                    Icon = new I18nAssetReference
                    {
                        AssetGuid = "abcdef0123456789abcdef0123456789",
                        LocalFileId = "21300000",
                    },
                },
            },
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
                                LocalFileId = "21300000",
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
                                  "defaultLocale": "en",
                                  "locales": [
                                    {
                                      "id": "en",
                                      "displayName": "English",
                                      "culture": "en-US"
                                    },
                                    {
                                      "id": "ru",
                                      "displayName": "Русский",
                                      "culture": "ru-RU",
                                      "fallback": "en",
                                      "icon": {
                                        "assetGuid": "abcdef0123456789abcdef0123456789",
                                        "localFileId": "21300000"
                                      }
                                    }
                                  ],
                                  "entries": [
                                    {
                                      "id": "1",
                                      "path": "Reports.ContextMenu.ReportNicknameButton",
                                      "locales": {
                                        "en": {
                                          "asset": {
                                            "assetGuid": "0123456789abcdef0123456789abcdef",
                                            "localFileId": "21300000"
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
                              "defaultLocale": "en",
                              "locales": [
                                {
                                  "id": "en",
                                  "displayName": "English",
                                  "culture": "en-US"
                                }
                              ],
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
        Assert.Equal("en", catalog.DefaultLocale);
        I18nLocaleDefinition locale = Assert.Single(catalog.Locales);
        Assert.Equal("en", locale.Id);
        Assert.Equal("English", locale.DisplayName);
        Assert.Equal("en-US", locale.Culture);
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
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new()
                {
                    Id = "en",
                    DisplayName = "English",
                    Culture = "en-US",
                    Icon = new I18nAssetReference
                    {
                        AssetGuid = "0123456789abcdef0123456789abcdef",
                        LocalFileId = "21300000",
                    },
                },
            },
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
                                LocalFileId = "21300000",
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
        Assert.Equal("21300000", entry.Locales["en"].Asset!.LocalFileId);
        Assert.Equal("0123456789abcdef0123456789abcdef", result.Locales[0].Icon!.AssetGuid);
        Assert.Equal("21300000", result.Locales[0].Icon!.LocalFileId);
    }

    [Fact]
    public void Serialize_UnsortedEntries_WritesNaturalCanonicalOrderWithoutModifyingCatalog()
    {
        I18nEntry tank10 = CreateEntry("10", "Units.Tank10.Title");
        I18nEntry tank02 = CreateEntry("2", "Units.Tank02.Title");
        I18nEntry tank2 = CreateEntry("1", "Units.Tank2.Title");
        var catalog = new I18nCatalog
        {
            DefaultLocale = "en",
            Locales = new List<I18nLocaleDefinition>
            {
                new() { Id = "en", DisplayName = "English", Culture = "en-US" },
            },
            Entries = new List<I18nEntry> { tank10, tank02, tank2 },
        };

        string json = I18nCatalogJson.Serialize(catalog);
        I18nCatalog serializedCatalog = I18nCatalogJson.Deserialize(json);

        Assert.Equal(
            new[] { "Units.Tank2.Title", "Units.Tank02.Title", "Units.Tank10.Title" },
            serializedCatalog.Entries.Select(entry => entry.Path));
        Assert.Equal(new[] { tank10, tank02, tank2 }, catalog.Entries);
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

    [Theory]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "locales": [],
          "entries": []
        }
        """, I18nValidationCodes.MissingDefaultLocale)]
    [InlineData(
        """
        {
          "schemaVersion": 1,
          "defaultLocale": "en",
          "entries": []
        }
        """, I18nValidationCodes.MissingLocaleDefinitions)]
    public void Deserialize_MissingLocaleContract_LeavesProblemForValidation(
        string json,
        string expectedCode)
    {
        I18nCatalog catalog = I18nCatalogJson.Deserialize(json);

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);

        Assert.True(validation.Contains(expectedCode));
    }

    [Fact]
    public void Deserialize_LocaleWithoutCulture_LeavesProblemForValidation()
    {
        const string json =
            """
            {
              "schemaVersion": 1,
              "defaultLocale": "en",
              "locales": [
                {
                  "id": "en",
                  "displayName": "English"
                }
              ],
              "entries": []
            }
            """;

        I18nCatalog catalog = I18nCatalogJson.Deserialize(json);

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);

        Assert.True(validation.Contains(I18nValidationCodes.MissingLocaleCulture));
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

    private static I18nEntry CreateEntry(string id, string path)
    {
        return new I18nEntry
        {
            Id = id,
            Path = path,
        };
    }
}
