#nullable enable

using System;
using System.Collections.Generic;

namespace GreenBox.I18n.Unity.Editor.Compilation
{
    /// <summary>
    /// Publishes Unity catalog compilation results to editor tools.
    /// </summary>
    public static class I18nCatalogCompilationEvents
    {
        private static readonly Dictionary<int, I18nCatalogCompilationResult> LastResults = new();

        /// <summary>
        /// Occurs after every manual or automatic compilation attempt.
        /// </summary>
        public static event Action<I18nCatalogAsset, I18nCatalogCompilationResult>? CompilationFinished;

        /// <summary>
        /// Records and publishes a compilation result.
        /// </summary>
        internal static void Publish(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            LastResults[catalogAsset.GetInstanceID()] = result;
            CompilationFinished?.Invoke(catalogAsset, result);
        }

        /// <summary>
        /// Gets the last result produced for a catalog during this editor session.
        /// </summary>
        internal static bool TryGetLastResult(
            I18nCatalogAsset catalogAsset,
            out I18nCatalogCompilationResult? result)
        {
            return LastResults.TryGetValue(catalogAsset.GetInstanceID(), out result);
        }
    }
}
