#nullable enable

using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Forwards imported, moved, and deleted serialized assets to the automatic usage scanner.
    /// </summary>
    internal sealed class I18nUsageAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            string[] changedAssets = new string[importedAssets.Length + movedAssets.Length];
            importedAssets.CopyTo(changedAssets, 0);
            movedAssets.CopyTo(changedAssets, importedAssets.Length);

            string[] removedAssets = new string[deletedAssets.Length + movedFromAssetPaths.Length];
            deletedAssets.CopyTo(removedAssets, 0);
            movedFromAssetPaths.CopyTo(removedAssets, deletedAssets.Length);

            I18nUsageAutoScanner.QueueAssetChanges(changedAssets, removedAssets);
        }
    }
}
