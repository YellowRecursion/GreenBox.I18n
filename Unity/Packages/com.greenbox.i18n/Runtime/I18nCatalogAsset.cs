using System.Collections.Generic;
using UnityEngine;

namespace GreenBox.I18n.Unity
{
    /// <summary>
    /// Stores generated runtime data and Unity objects compiled from the source catalog.
    /// </summary>
    public sealed class I18nCatalogAsset : ScriptableObject
    {
        /// <summary>
        /// Logical Resources path used by the automatic runtime loader.
        /// </summary>
        internal const string ResourcesPath = "greenbox-i18n";

        [SerializeField, HideInInspector]
        private string _sourceHash = string.Empty;

        [SerializeField, HideInInspector]
        private byte[] _compiledCatalog = System.Array.Empty<byte>();

        [SerializeField, HideInInspector]
        private List<I18nAssetBinding> _assetBindings = new();

        /// <summary>
        /// Gets whether this asset contains a compiled runtime catalog.
        /// </summary>
        internal bool HasCompiledCatalog => _compiledCatalog.Length > 0;

        /// <summary>
        /// Gets the hash of the source used to produce the compiled asset bindings.
        /// </summary>
        internal string SourceHash => _sourceHash;

        /// <summary>
        /// Gets the Unity objects compiled from source asset references.
        /// </summary>
        internal IReadOnlyList<I18nAssetBinding> AssetBindings => _assetBindings;

        /// <summary>
        /// Loads the prepared runtime catalog without parsing source JSON or MessageFormat.
        /// </summary>
        /// <returns>The deserialized engine-independent catalog.</returns>
        internal I18nCompiledCatalog DeserializeCompiledCatalog()
        {
            return I18nCompiledCatalogBinary.Deserialize(_compiledCatalog);
        }

        /// <summary>
        /// Replaces the generated runtime data after a successful editor compilation.
        /// </summary>
        internal void ReplaceCompiledData(
            string sourceHash,
            byte[] compiledCatalog,
            List<I18nAssetBinding> assetBindings)
        {
            _sourceHash = sourceHash;
            _compiledCatalog = compiledCatalog;
            _assetBindings = assetBindings;
        }
    }
}
