using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GreenBox.I18n
{
    /// <summary>
    /// Represents the complete source localization catalog stored in a single file.
    /// </summary>
    [Serializable]
    public sealed class I18nCatalog
    {
        /// <summary>
        /// Gets or sets the version of the JSON data contract used by this file.
        /// </summary>
        [JsonProperty("schemaVersion", Order = 0)]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the ordered collection of localisation entries.
        /// </summary>
        [JsonProperty("entries", Order = 1)]
        public List<I18nEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Represents a localisation entry with a stable identity and locale-specific values.
    /// </summary>
    [Serializable]
    public sealed class I18nEntry
    {
        /// <summary>
        /// Gets or sets the immutable globally unique identifier of the entry.
        /// </summary>
        [JsonProperty("id", Order = 0)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mutable full logical path of the entry.
        /// </summary>
        [JsonProperty("path", Order = 1)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional contextual note for developers, translators, and tooling.
        /// </summary>
        [JsonProperty("comment", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public string? Comment { get; set; }

        /// <summary>
        /// Gets or sets the values associated with locale identifiers.
        /// </summary>
        [JsonProperty("locales", Order = 3)]
        public Dictionary<string, I18nLocaleValue> Locales { get; set; } = new();
    }

    /// <summary>
    /// Represents the text and assets assigned to an entry for a specific locale.
    /// </summary>
    [Serializable]
    public sealed class I18nLocaleValue
    {
        /// <summary>
        /// Gets or sets the localized text, or <see langword="null"/> when the entry has no text.
        /// </summary>
        [JsonProperty("text", Order = 0)]
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the localized Unity asset reference, or <see langword="null"/> when the entry has no asset.
        /// </summary>
        [JsonProperty("asset", Order = 1)]
        public I18nAssetReference? Asset { get; set; }
    }

    /// <summary>
    /// Identifies a Unity asset without depending on its mutable project path.
    /// </summary>
    [Serializable]
    public sealed class I18nAssetReference
    {
        /// <summary>
        /// Gets or sets the GUID stored in the Unity asset's meta file.
        /// </summary>
        [JsonProperty("assetGuid", Order = 0)]
        public string AssetGuid { get; set; } = string.Empty;
    }
}
