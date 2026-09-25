#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Compilation
{
    /// <summary>
    /// Publishes Unity catalog compilation results to editor tools.
    /// </summary>
    public static class I18nCatalogCompilationEvents
    {
#if UNITY_6000_4_OR_NEWER
        private static readonly Dictionary<EntityId, I18nCatalogCompilationResult> LastResults = new();
#else
        private static readonly Dictionary<int, I18nCatalogCompilationResult> LastResults = new();
#endif

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
#if UNITY_6000_4_OR_NEWER
            LastResults[catalogAsset.GetEntityId()] = result;
#else
            LastResults[catalogAsset.GetInstanceID()] = result;
#endif
            CompilationFinished?.Invoke(catalogAsset, result);
        }

        /// <summary>
        /// Gets the last result produced for a catalog during this editor session.
        /// </summary>
        internal static bool TryGetLastResult(
            I18nCatalogAsset catalogAsset,
            out I18nCatalogCompilationResult? result)
        {
#if UNITY_6000_4_OR_NEWER
            return LastResults.TryGetValue(catalogAsset.GetEntityId(), out result);
#else
            return LastResults.TryGetValue(catalogAsset.GetInstanceID(), out result);
#endif
        }
    }
}
