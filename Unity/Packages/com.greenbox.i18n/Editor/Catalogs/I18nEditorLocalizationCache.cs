#nullable enable

using System;
using System.Collections.Generic;
using GreenBox.I18n.Unity.Assets;
using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Catalogs
{
    /// <summary>
    /// Holds immutable compiled data and one lightweight runtime view per requested locale.
    /// </summary>
    internal sealed class I18nEditorLocalizationContext
    {
        private readonly I18nCompiledCatalog _catalog;
        private readonly I18nUnityAssetResolver _assetResolver;
        private readonly Dictionary<string, I18nRuntime> _runtimes =
            new(StringComparer.Ordinal);
        private readonly I18nRuntime _defaultRuntime;

        internal I18nEditorLocalizationContext(I18nCatalogAsset catalogAsset)
        {
            if (!catalogAsset)
            {
                throw new ArgumentNullException(nameof(catalogAsset));
            }

            if (!catalogAsset.HasCompiledCatalog)
            {
                throw new I18nEditorException(
                    "The generated GreenBox I18n catalog has not been compiled yet.");
            }

            _catalog = catalogAsset.DeserializeCompiledCatalog();
            _assetResolver = new I18nUnityAssetResolver(catalogAsset.AssetBindings);
            _defaultRuntime = new I18nRuntime(_catalog);
            _runtimes.Add(_defaultRuntime.DefaultLocale.Id, _defaultRuntime);
        }

        internal I18nRuntime GetRuntime(string? localeId)
        {
            if (localeId == null)
            {
                return _defaultRuntime;
            }

            if (_runtimes.TryGetValue(localeId, out I18nRuntime runtime))
            {
                return runtime;
            }

            runtime = new I18nRuntime(_catalog, localeId);
            _runtimes.Add(localeId, runtime);
            return runtime;
        }

        internal UnityEngine.Object ResolveAsset(I18nAssetReference reference)
        {
            return _assetResolver.Resolve(reference);
        }
    }

    /// <summary>
    /// Owns the active project preview snapshot and invalidates it after catalog changes.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nEditorLocalizationCache
    {
        private static I18nCatalogAsset? _catalogAsset;
        private static I18nEditorLocalizationContext? _context;

        static I18nEditorLocalizationCache()
        {
            I18nCatalogCompilationEvents.CompilationFinished += HandleCompilationFinished;
            I18nProjectSettings.SourceCatalogChanged += Clear;
        }

        internal static I18nEditorLocalizationContext Current
        {
            get
            {
                if (_context != null && _catalogAsset)
                {
                    return _context;
                }

                I18nCatalogAsset? catalogAsset = I18nProjectSettings.instance.ProjectCatalog;
                if (!catalogAsset)
                {
                    throw new I18nEditorException(
                        "The generated GreenBox I18n project catalog is unavailable. " +
                        "Let project setup repair it first.");
                }

                if (I18nCatalogCompiler.GetState(catalogAsset) !=
                    I18nCatalogCompilationState.UpToDate)
                {
                    throw new I18nEditorException(
                        "The generated GreenBox I18n project catalog is not up to date yet.");
                }

                _catalogAsset = catalogAsset;
                _context = new I18nEditorLocalizationContext(catalogAsset);
                return _context;
            }
        }

        private static void HandleCompilationFinished(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            Clear();
        }

        private static void Clear()
        {
            _catalogAsset = null;
            _context = null;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                global::I18n.ResetRuntime();
            }

            I18nRuntimeDiagnosticReporter.Clear();
        }
    }
}
