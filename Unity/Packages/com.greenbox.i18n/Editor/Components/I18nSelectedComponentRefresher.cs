#nullable enable

using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Components
{
    /// <summary>
    /// Refreshes localized content on the currently selected objects after catalog compilation.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nSelectedComponentRefresher
    {
        private static bool _isScheduled;

        static I18nSelectedComponentRefresher()
        {
            I18nCatalogCompilationEvents.CompilationFinished += HandleCompilationFinished;
        }

        internal static void RefreshSelectedObjects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameObject[] selectedGameObjects = Selection.gameObjects;
            for (int objectIndex = 0; objectIndex < selectedGameObjects.Length; objectIndex++)
            {
                I18nComponent[] components =
                    selectedGameObjects[objectIndex].GetComponents<I18nComponent>();
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    components[componentIndex].ForceRefresh();
                }
            }
        }

        private static void HandleCompilationFinished(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            if (!result.IsSuccess ||
                catalogAsset != I18nProjectSettings.instance.ProjectCatalog ||
                _isScheduled)
            {
                return;
            }

            _isScheduled = true;
            EditorApplication.delayCall += RefreshAfterCompilation;
        }

        private static void RefreshAfterCompilation()
        {
            _isScheduled = false;
            RefreshSelectedObjects();
        }
    }
}
