#nullable enable

using System;
using System.Collections.Generic;
using GreenBox.I18n;
using UnityEngine;

namespace GreenBox.I18n.Unity.Assets
{
    /// <summary>
    /// Resolves engine-independent asset references to compiled Unity objects.
    /// </summary>
    internal sealed class I18nUnityAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetReferenceKey, UnityEngine.Object> _assets;

        /// <summary>
        /// Builds an immutable lookup from serialized catalog bindings.
        /// </summary>
        internal I18nUnityAssetResolver(IReadOnlyList<I18nAssetBinding> bindings)
        {
            var assets = new Dictionary<AssetReferenceKey, UnityEngine.Object>();

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                I18nAssetBinding binding = bindings[bindingIndex];
                if (!binding.Asset)
                {
                    throw new InvalidOperationException(
                        $"Compiled localization asset binding for GUID '{binding.AssetGuid}' " +
                        "does not contain a Unity object. Recompile the catalog.");
                }

                var key = new AssetReferenceKey(binding.AssetGuid, binding.LocalFileId);
                if (!assets.TryAdd(key, binding.Asset))
                {
                    throw new InvalidOperationException(
                        $"Compiled localization catalog contains duplicate asset binding " +
                        $"for GUID '{binding.AssetGuid}' and local file ID '{binding.LocalFileId}'.");
                }
            }

            _assets = assets;
        }

        /// <summary>
        /// Resolves a catalog asset reference.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the reference is absent from the compiled bindings.
        /// </exception>
        internal UnityEngine.Object Resolve(I18nAssetReference reference)
        {
            var key = new AssetReferenceKey(
                reference.AssetGuid,
                reference.LocalFileId ?? string.Empty);

            if (_assets.TryGetValue(key, out UnityEngine.Object asset) && asset)
            {
                return asset;
            }

            throw new InvalidOperationException(
                $"Localization asset reference with GUID '{reference.AssetGuid}' and local file ID " +
                $"'{reference.LocalFileId ?? string.Empty}' was not compiled. Recompile the catalog.");
        }

        private readonly struct AssetReferenceKey : IEquatable<AssetReferenceKey>
        {
            internal AssetReferenceKey(string assetGuid, string localFileId)
            {
                AssetGuid = assetGuid;
                LocalFileId = localFileId;
            }

            private string AssetGuid { get; }

            private string LocalFileId { get; }

            /// <inheritdoc />
            public bool Equals(AssetReferenceKey other)
            {
                return
                    string.Equals(AssetGuid, other.AssetGuid, StringComparison.Ordinal) &&
                    string.Equals(LocalFileId, other.LocalFileId, StringComparison.Ordinal);
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return obj is AssetReferenceKey other && Equals(other);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (StringComparer.Ordinal.GetHashCode(AssetGuid) * 397) ^
                        StringComparer.Ordinal.GetHashCode(LocalFileId);
                }
            }
        }
    }
}
