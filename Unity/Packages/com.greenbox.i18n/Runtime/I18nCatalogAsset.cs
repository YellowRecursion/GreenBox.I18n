using System.Collections.Generic;
using UnityEngine;

namespace GreenBox.I18n.Unity
{
    /// <summary>
    /// Stores a source catalog and the Unity objects compiled from its asset references.
    /// </summary>
    [CreateAssetMenu(fileName = "I18nCatalog", menuName = "GreenBox/I18n Catalog")]
    public sealed class I18nCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private TextAsset _sourceCatalog;

        [SerializeField, HideInInspector]
        private string _sourceHash = string.Empty;

        [SerializeField, HideInInspector]
        private List<I18nAssetBinding> _assetBindings = new();

        /// <summary>
        /// Gets the JSON source catalog represented by this asset.
        /// </summary>
        public TextAsset SourceCatalog => _sourceCatalog;

        /// <summary>
        /// Gets the hash of the source used to produce the compiled asset bindings.
        /// </summary>
        internal string SourceHash => _sourceHash;

        /// <summary>
        /// Gets the Unity objects compiled from source asset references.
        /// </summary>
        internal IReadOnlyList<I18nAssetBinding> AssetBindings => _assetBindings;

        /// <summary>
        /// Deserializes the current JSON source without modifying it.
        /// </summary>
        /// <returns>The deserialized engine-independent catalog.</returns>
        internal I18nCatalog Deserialize()
        {
            return I18nCatalogJson.Deserialize(_sourceCatalog.text);
        }

        /// <summary>
        /// Replaces the generated runtime data after a successful editor compilation.
        /// </summary>
        internal void ReplaceCompiledData(
            string sourceHash,
            List<I18nAssetBinding> assetBindings)
        {
            _sourceHash = sourceHash;
            _assetBindings = assetBindings;
        }
    }
}
