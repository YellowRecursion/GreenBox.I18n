using System;
using UnityEngine;

namespace GreenBox.I18n.Unity
{
    /// <summary>
    /// Associates an engine-independent asset reference with its serialized Unity object.
    /// </summary>
    [Serializable]
    internal sealed class I18nAssetBinding
    {
        [SerializeField]
        private string _assetGuid = string.Empty;

        [SerializeField]
        private string _localFileId = string.Empty;

        [SerializeField]
        private UnityEngine.Object _asset;

        /// <summary>
        /// Gets the GUID stored in the Unity asset's meta file.
        /// </summary>
        internal string AssetGuid => _assetGuid;

        /// <summary>
        /// Gets the local file identifier, or an empty string for a main asset without one.
        /// </summary>
        internal string LocalFileId => _localFileId;

        /// <summary>
        /// Gets the Unity object serialized into the runtime catalog asset.
        /// </summary>
        internal UnityEngine.Object Asset => _asset;
    }
}
