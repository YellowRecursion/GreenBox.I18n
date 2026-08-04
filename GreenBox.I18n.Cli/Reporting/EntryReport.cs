using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal sealed class EntryReport
{
    public required string Id { get; init; }

    public required string Path { get; init; }

    public string? Comment { get; init; }

    public required IReadOnlyDictionary<string, LocaleValueReport> Locales { get; init; }

    public static EntryReport Create(I18nEntry entry)
    {
        return new EntryReport
        {
            Id = entry.Id,
            Path = entry.Path,
            Comment = entry.Comment,
            Locales = entry.Locales
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => LocaleValueReport.Create(pair.Value),
                    StringComparer.Ordinal),
        };
    }
}

internal sealed class LocaleValueReport
{
    public string? Text { get; init; }

    public AssetReferenceReport? Asset { get; init; }

    public static LocaleValueReport Create(I18nLocaleValue value)
    {
        return new LocaleValueReport
        {
            Text = value.Text,
            Asset = value.Asset == null ? null : AssetReferenceReport.Create(value.Asset),
        };
    }
}

internal sealed class AssetReferenceReport
{
    public required string AssetGuid { get; init; }

    public string? LocalFileId { get; init; }

    public static AssetReferenceReport Create(I18nAssetReference asset)
    {
        return new AssetReferenceReport
        {
            AssetGuid = asset.AssetGuid,
            LocalFileId = asset.LocalFileId,
        };
    }
}
