#nullable enable

using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Provides safe operations for one entry inside an active <see cref="I18nCatalogEdit"/>.
    /// </summary>
    public sealed class I18nEditorEntry
    {
        private readonly I18nCatalogEdit _edit;
        private readonly long _id;

        internal I18nEditorEntry(I18nCatalogEdit edit, long id, bool wasCreated)
        {
            _edit = edit;
            _id = id;
            WasCreated = wasCreated;
        }

        /// <summary>Gets the stable Unity key assigned to the entry.</summary>
        public I18nKey Key => new I18nKey(_id);

        /// <summary>Gets the entry's current logical path.</summary>
        public string Path => _edit.GetEntry(_id).Path;

        /// <summary>Gets whether this transaction created the entry.</summary>
        public bool WasCreated { get; }

        /// <summary>Sets the optional developer and translator comment.</summary>
        public I18nEditorEntry SetComment(string? comment)
        {
            _edit.SetComment(_id, comment);
            return this;
        }

        /// <summary>Sets or clears text for a declared locale.</summary>
        public I18nEditorEntry SetText(string localeId, string? text)
        {
            _edit.SetText(_id, localeId, text);
            return this;
        }

        /// <summary>Sets or clears text for the catalog default locale.</summary>
        public I18nEditorEntry SetDefaultText(string? text)
        {
            return SetText(_edit.DefaultLocale, text);
        }

        /// <summary>Sets or clears a persistent Unity asset for a declared locale.</summary>
        public I18nEditorEntry SetAsset(string localeId, UnityEngine.Object? asset)
        {
            _edit.SetAsset(_id, localeId, CreateAssetReference(asset));
            return this;
        }

        /// <summary>Sets or clears a persistent Unity asset for the catalog default locale.</summary>
        public I18nEditorEntry SetDefaultAsset(UnityEngine.Object? asset)
        {
            return SetAsset(_edit.DefaultLocale, asset);
        }

        /// <summary>Changes the path while preserving the stable entry key.</summary>
        public I18nEditorEntry Move(string path)
        {
            _edit.Move(_id, path);
            return this;
        }

        /// <summary>Removes the entry from the catalog.</summary>
        public void Remove()
        {
            _edit.Remove(_id);
        }

        private static I18nAssetReference? CreateAssetReference(UnityEngine.Object? asset)
        {
            if (!asset)
            {
                return null;
            }

            if (!EditorUtility.IsPersistent(asset) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out string assetGuid,
                    out long localFileId))
            {
                throw new ArgumentException(
                    $"Unity object '{asset.name}' must be a persistent project asset.",
                    nameof(asset));
            }

            return new I18nAssetReference
            {
                AssetGuid = assetGuid,
                LocalFileId = localFileId.ToString(CultureInfo.InvariantCulture),
            };
        }
    }
}
