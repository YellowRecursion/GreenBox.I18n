#nullable enable

using System.Globalization;
using GreenBox.I18n.Unity.Editor.Catalogs;
using GreenBox.I18n.Unity.Editor.Pickers;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Drawers
{
    /// <summary>
    /// Draws localization keys as human-readable catalog paths.
    /// </summary>
    [CustomPropertyDrawer(typeof(I18nKey))]
    internal sealed class I18nKeyPropertyDrawer : PropertyDrawer
    {
        private const string IdPropertyName = "_greenBoxI18nEntryId";

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty idProperty = property.FindPropertyRelative(IdPropertyName);
            Rect fieldPosition = EditorGUI.PrefixLabel(position, label);
            GUIContent content;
            FieldState state;

            if (property.hasMultipleDifferentValues)
            {
                content = new GUIContent("—");
                state = FieldState.None;
            }
            else
            {
                content = GetContent(idProperty.longValue, out state);
            }

            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            ApplyStateColors(state);
            if (EditorGUI.DropdownButton(fieldPosition, content, FocusType.Keyboard, EditorStyles.popup))
            {
                ShowDropdown(fieldPosition, property);
            }

            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;

            EditorGUI.EndProperty();
        }

        private static GUIContent GetContent(long id, out FieldState state)
        {
            if (id == 0)
            {
                state = FieldState.None;
                return new GUIContent("none");
            }

            I18nCatalog? catalog = I18nEditorCatalogProvider.GetCatalog(out string? error);
            if (catalog == null)
            {
                state = FieldState.Unavailable;
                return new GUIContent("catalog unavailable", error);
            }

            I18nEntry? entry = id > 0 ? catalog.FindById(id) : null;
            if (entry != null)
            {
                state = FieldState.Valid;
                return new GUIContent(entry.Path);
            }

            state = FieldState.Missing;
            return new GUIContent($"missing: {id.ToString(CultureInfo.InvariantCulture)}");
        }

        private static void ShowDropdown(Rect position, SerializedProperty property)
        {
            I18nCatalog? catalog = I18nEditorCatalogProvider.GetCatalog(out _);
            Object[] targetObjects = property.serializedObject.targetObjects;
            string propertyPath = property.propertyPath;
            var dropdown = new I18nKeyDropdown(
                catalog,
                id => AssignId(targetObjects, propertyPath, id));
            dropdown.Show(position);
        }

        private static void AssignId(Object[] targetObjects, string propertyPath, long id)
        {
            var serializedObject = new SerializedObject(targetObjects);
            serializedObject.Update();
            SerializedProperty keyProperty = serializedObject.FindProperty(propertyPath);
            keyProperty.FindPropertyRelative(IdPropertyName).longValue = id;
            serializedObject.ApplyModifiedProperties();
        }

        private static void ApplyStateColors(FieldState state)
        {
            switch (state)
            {
                case FieldState.Missing:
                    GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                    break;
                case FieldState.Unavailable:
                    GUI.backgroundColor = new Color(1f, 0.75f, 0.35f);
                    break;
            }
        }

        private enum FieldState
        {
            None,
            Valid,
            Missing,
            Unavailable
        }
    }
}
