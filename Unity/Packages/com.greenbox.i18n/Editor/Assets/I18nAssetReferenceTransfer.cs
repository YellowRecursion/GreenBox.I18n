#nullable enable

using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Assets
{
    /// <summary>
    /// Exposes the selected Unity object as a portable i18n asset-reference payload.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nAssetReferenceTransfer
    {
        private const string ClipboardFormat = "greenbox.i18n.asset-reference";
        private const int ClipboardFormatVersion = 1;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        static I18nAssetReferenceTransfer()
        {
            Selection.selectionChanged += WriteActiveSelection;
            EditorApplication.projectWindowItemInstanceOnGUI += HandleProjectWindowItemGui;
            EditorApplication.delayCall += WriteActiveSelection;
        }

        /// <summary>
        /// Copies the exact selected Unity object reference from the Project window.
        /// </summary>
        [MenuItem("Assets/Copy i18n Asset Reference", false, 2000)]
        private static void CopySelectedReference()
        {
            if (!TryCreatePayload(Selection.activeObject, out I18nAssetReferencePayload? payload))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = JsonConvert.SerializeObject(payload, JsonSettings);
            Debug.Log($"Copied i18n asset reference for '{payload.ObjectName}'.");
        }

        /// <summary>
        /// Determines whether the current Project window selection can be copied.
        /// </summary>
        /// <returns><see langword="true"/> when Unity can identify the selected object.</returns>
        [MenuItem("Assets/Copy i18n Asset Reference", true)]
        private static bool CanCopySelectedReference()
        {
            return TryCreatePayload(Selection.activeObject, out _);
        }

        private static void WriteActiveSelection()
        {
            WriteActiveSelection(Selection.activeObject);
        }

        private static void HandleProjectWindowItemGui(int instanceId, Rect selectionRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDrag && selectionRect.Contains(currentEvent.mousePosition))
            {
                WriteActiveSelection(EditorUtility.InstanceIDToObject(instanceId));
            }
        }

        private static void WriteActiveSelection(UnityEngine.Object selectedObject)
        {
            string destinationPath = GetActiveSelectionPath();
            if (!TryCreatePayload(selectedObject, out I18nAssetReferencePayload? payload))
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                return;
            }

            string directory = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = destinationPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(payload, JsonSettings));
            File.Copy(temporaryPath, destinationPath, true);
            File.Delete(temporaryPath);
        }

        private static bool TryCreatePayload(
            UnityEngine.Object selectedObject,
            out I18nAssetReferencePayload? payload)
        {
            if (!selectedObject ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    selectedObject,
                    out string assetGuid,
                    out long localFileId))
            {
                payload = null;
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                payload = null;
                return false;
            }

            payload = new I18nAssetReferencePayload(
                ClipboardFormat,
                ClipboardFormatVersion,
                assetGuid,
                localFileId.ToString(CultureInfo.InvariantCulture),
                assetPath,
                selectedObject.name,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return true;
        }

        private static string GetActiveSelectionPath()
        {
            string projectDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectDirectory, "Library", "GreenBox.I18n", "active-selection.json");
        }

        private sealed class I18nAssetReferencePayload
        {
            public I18nAssetReferencePayload(
                string format,
                int version,
                string assetGuid,
                string localFileId,
                string assetPath,
                string objectName,
                long updatedAtUnixMilliseconds)
            {
                Format = format;
                Version = version;
                AssetGuid = assetGuid;
                LocalFileId = localFileId;
                AssetPath = assetPath;
                ObjectName = objectName;
                UpdatedAtUnixMilliseconds = updatedAtUnixMilliseconds;
            }

            public string Format { get; }

            public int Version { get; }

            public string AssetGuid { get; }

            public string LocalFileId { get; }

            public string AssetPath { get; }

            public string ObjectName { get; }

            public long UpdatedAtUnixMilliseconds { get; }
        }
    }
}
