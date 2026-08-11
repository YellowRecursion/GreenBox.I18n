using System.Globalization;

namespace GreenBox.I18n
{
    /// <summary>
    /// Describes an immutable locale available to an <see cref="I18nRuntime"/>.
    /// </summary>
    public sealed class I18nRuntimeLocale
    {
        private readonly I18nAssetReference? _icon;

        internal I18nRuntimeLocale(I18nLocaleDefinition definition)
        {
            Id = definition.Id;
            DisplayName = definition.DisplayName;
            Culture = CultureInfo.GetCultureInfo(definition.Culture);
            _icon = CloneAsset(definition.Icon);
        }

        internal I18nRuntimeLocale(
            I18nCompiledCatalogStorage catalog,
            I18nCompiledLocaleRecord locale)
        {
            Id = catalog.GetString(locale.Id);
            DisplayName = catalog.GetString(locale.DisplayName);
            Culture = CultureInfo.GetCultureInfo(catalog.GetString(locale.CultureName));
            _icon = catalog.CreateAsset(locale.Icon);
        }

        /// <summary>
        /// Gets the stable locale identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the human-readable locale name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the culture used for locale-aware formatting.
        /// </summary>
        public CultureInfo Culture { get; }

        /// <summary>
        /// Gets the optional icon asset shown for the locale.
        /// </summary>
        public I18nAssetReference? Icon => CloneAsset(_icon);

        private static I18nAssetReference? CloneAsset(I18nAssetReference? asset)
        {
            if (asset == null)
            {
                return null;
            }

            return new I18nAssetReference
            {
                AssetGuid = asset.AssetGuid,
                LocalFileId = asset.LocalFileId,
            };
        }
    }
}
