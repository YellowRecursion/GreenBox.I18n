#nullable enable

using System;
using System.Globalization;
using GreenBox.I18n.Unity.Assets;
using GreenBox.I18n.Unity.Editor.Catalogs;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Assets
{
    /// <summary>
    /// Resolves default-locale Unity assets for editor previews from compiled catalog bindings.
    /// </summary>
    /// <typeparam name="TAsset">Expected Unity asset type.</typeparam>
    internal sealed class I18nEditorAssetResolver<TAsset>
        where TAsset : UnityEngine.Object
    {
        private I18nCatalog? _catalog;
        private I18nCatalogAsset? _catalogAsset;
        private I18nRuntime? _runtime;
        private I18nUnityAssetResolver? _assetResolver;

        /// <summary>
        /// Resolves an asset for an entry ID without changing the active runtime locale.
        /// </summary>
        internal bool TryResolve(long id, out TAsset? asset, out string? error)
        {
            asset = null;
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

            I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ProjectCatalog;
            if (!catalogAsset)
            {
                Reset();
                error = "Generated localization runtime data is unavailable.";
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

                UnityEngine.Object resolvedAsset = _assetResolver!.Resolve(reference);
                if (resolvedAsset is not TAsset typedAsset)
                {
                    error =
                        $"Localization asset for entry ID '{id.ToString(CultureInfo.InvariantCulture)}' " +
                        $"is '{resolvedAsset.GetType().FullName}', not '{typeof(TAsset).FullName}'.";
                    return false;
                }

                asset = typedAsset;
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
