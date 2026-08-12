#nullable enable

using System;
using GreenBox.I18n.Unity.Editor.Catalogs;

namespace GreenBox.I18n.Unity.Editor
{
    public static partial class I18nEditor
    {
        /// <summary>
        /// Gets a localized Unity object using the catalog default locale or an explicitly requested locale.
        /// </summary>
        public static UnityEngine.Object? Asset(long id, string? localeId = null)
        {
            if (id == 0)
            {
                return null;
            }

            I18nEditorLocalizationContext context = I18nEditorLocalizationCache.Current;
            I18nAssetReference? reference = context.GetRuntime(localeId).Asset(id);
            return reference == null ? null : context.ResolveAsset(reference);
        }

        /// <summary>Gets a localized Unity object of the requested type.</summary>
        public static T? Asset<T>(long id, string? localeId = null)
            where T : UnityEngine.Object
        {
            UnityEngine.Object? asset = Asset(id, localeId);
            if (!asset)
            {
                return null;
            }

            if (asset is T typedAsset)
            {
                return typedAsset;
            }

            throw new InvalidCastException(
                $"Localization asset for entry ID '{id}' is '{asset.GetType().FullName}', " +
                $"not '{typeof(T).FullName}'.");
        }

        /// <summary>Attempts to get a localized Unity object of the requested type.</summary>
        public static bool TryGetAsset<T>(
            long id,
            out T? asset,
            string? localeId = null)
            where T : UnityEngine.Object
        {
            UnityEngine.Object? resolvedAsset = Asset(id, localeId);
            asset = resolvedAsset as T;
            return asset;
        }
    }
}
