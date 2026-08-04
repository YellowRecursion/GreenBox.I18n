#nullable enable

using System;
using System.Globalization;
using GreenBox.I18n.Unity.Assets;
using GreenBox.I18n.Unity.Editor.Catalogs;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Sprites
{
    /// <summary>
    /// Resolves default-locale sprites for editor previews from compiled catalog bindings.
    /// </summary>
    internal sealed class I18nEditorSpriteResolver
    {
        private I18nCatalog? _catalog;
        private I18nCatalogAsset? _catalogAsset;
        private I18nRuntime? _runtime;
        private I18nUnityAssetResolver? _assetResolver;

        /// <summary>
        /// Resolves a sprite for an entry ID without changing the active runtime locale.
        /// </summary>
        internal bool TryResolve(long id, out Sprite? sprite, out string? error)
        {
            sprite = null;
            error = null;

            if (id == 0)
            {
                return true;
            }

            I18nCatalog? catalog = I18nEditorCatalogProvider.GetCatalog(out error);
            if (catalog == null)
            {
                Reset();
                return false;
            }

            if (id < 0 || catalog.FindById(id) == null)
            {
                error = $"Localization entry ID '{id.ToString(CultureInfo.InvariantCulture)}' does not exist.";
                return false;
            }

            I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ActiveCatalog;
            if (!catalogAsset)
            {
                Reset();
                error = "Active localization catalog is not configured.";
                return false;
            }

            try
            {
                EnsureRuntime(catalog, catalogAsset);
                I18nAssetReference? reference = _runtime!.Asset(id);
                if (reference == null)
                {
                    return true;
                }

                UnityEngine.Object asset = _assetResolver!.Resolve(reference);
                if (asset is not Sprite resolvedSprite)
                {
                    error =
                        $"Localization asset for entry ID '{id.ToString(CultureInfo.InvariantCulture)}' " +
                        $"is '{asset.GetType().FullName}', not '{typeof(Sprite).FullName}'.";
                    return false;
                }

                sprite = resolvedSprite;
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException ||
                exception is ArgumentOutOfRangeException)
            {
                Reset();
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Clears cached runtime and compiled binding state.
        /// </summary>
        internal void Reset()
        {
            _catalog = null;
            _catalogAsset = null;
            _runtime = null;
            _assetResolver = null;
        }

        private void EnsureRuntime(I18nCatalog catalog, I18nCatalogAsset catalogAsset)
        {
            if (ReferenceEquals(_catalog, catalog) && ReferenceEquals(_catalogAsset, catalogAsset))
            {
                return;
            }

            _catalog = catalog;
            _catalogAsset = catalogAsset;
            _runtime = new I18nRuntime(catalog);
            _assetResolver = new I18nUnityAssetResolver(catalogAsset.AssetBindings);
        }
    }
}
