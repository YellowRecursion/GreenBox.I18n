#nullable enable

using System;
using System.Globalization;
using GreenBox.I18n.Unity.Editor.Catalogs;
using GreenBox.I18n.Unity.Editor.Compilation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using I18nTextComponent = GreenBox.I18n.Unity.Text.I18nText;
using I18nTextTransformUtility = GreenBox.I18n.Unity.Text.I18nTextTransformUtility;
using LegacyText = UnityEngine.UI.Text;

namespace GreenBox.I18n.Unity.Editor.Text
{
    /// <summary>
    /// Draws an I18nText key and keeps its selected-object default-locale preview current.
    /// </summary>
    [CustomEditor(typeof(I18nTextComponent))]
    internal sealed class I18nTextInspector : UnityEditor.Editor
    {
        private const string KeyPropertyName = "_key";
        private const string TextTransformPropertyName = "_textTransform";
        private const string IdPropertyName = "_id";

        private SerializedProperty? _keyProperty;
        private SerializedProperty? _textTransformProperty;
        private I18nCatalog? _runtimeCatalog;
        private I18nRuntime? _previewRuntime;
        private string? _previewError;
        private bool _hasAmbiguousTarget;

        private I18nTextComponent I18nText => (I18nTextComponent)target;

        private void OnEnable()
        {
            _keyProperty = serializedObject.FindProperty(KeyPropertyName);
            _textTransformProperty = serializedObject.FindProperty(TextTransformPropertyName);
            I18nCatalogCompilationEvents.CompilationFinished += OnCompilationFinished;
            RefreshPreview();
        }

        private void OnDisable()
        {
            I18nCatalogCompilationEvents.CompilationFinished -= OnCompilationFinished;
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_keyProperty);
            EditorGUILayout.PropertyField(_textTransformProperty);
            serializedObject.ApplyModifiedProperties();

            RefreshPreview();
            DrawPreviewStatus();
        }

        private void RefreshPreview()
        {
            if (_keyProperty == null || !target)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            long id = _keyProperty.FindPropertyRelative(IdPropertyName).longValue;
            string? previewText = ResolvePreviewText(id);
            if (previewText == null)
            {
                return;
            }

            TMP_Text? textMeshPro = I18nText.GetComponent<TMP_Text>();
            LegacyText? legacyText = I18nText.GetComponent<LegacyText>();
            _hasAmbiguousTarget = textMeshPro && legacyText;

            if (textMeshPro)
            {
                ApplyPreview(textMeshPro, textMeshPro.text, previewText);
            }
            else if (legacyText)
            {
                ApplyPreview(legacyText, legacyText.text, previewText);
            }
            else
            {
                _previewError =
                    "I18nText requires a TextMeshPro or UnityEngine.UI.Text component on the same GameObject.";
            }
        }

        private string? ResolvePreviewText(long id)
        {
            _previewError = null;

            if (id == 0)
            {
                return i18n.NonePlaceholder;
            }

            I18nCatalog? catalog = I18nEditorCatalogProvider.GetCatalog(out string? error);
            if (catalog == null)
            {
                _runtimeCatalog = null;
                _previewRuntime = null;
                _previewError = error;
                return null;
            }

            if (id < 0 || catalog.FindById(id) == null)
            {
                return $"[missing: {id.ToString(CultureInfo.InvariantCulture)}]";
            }

            try
            {
                if (!ReferenceEquals(_runtimeCatalog, catalog))
                {
                    _runtimeCatalog = catalog;
                    _previewRuntime = new I18nRuntime(catalog);
                }

                return I18nTextTransformUtility.Apply(
                    _previewRuntime!.Text(id),
                    I18nText.TextTransform,
                    _previewRuntime.CurrentCulture);
            }
            catch (Exception exception)
            {
                _runtimeCatalog = null;
                _previewRuntime = null;
                _previewError = exception.Message;
                return null;
            }
        }

        private static void ApplyPreview(
            Component textComponent,
            string currentText,
            string previewText)
        {
            if (string.Equals(currentText, previewText, StringComparison.Ordinal))
            {
                return;
            }

            switch (textComponent)
            {
                case TMP_Text textMeshPro:
                    textMeshPro.text = previewText;
                    break;
                case LegacyText legacyText:
                    legacyText.text = previewText;
                    break;
            }

            EditorUtility.SetDirty(textComponent);
            if (textComponent.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(textComponent.gameObject.scene);
            }
        }

        private void DrawPreviewStatus()
        {
            if (_previewError != null)
            {
                EditorGUILayout.HelpBox(_previewError, MessageType.Error);
            }

            if (_hasAmbiguousTarget)
            {
                EditorGUILayout.HelpBox(
                    "Both TextMeshPro and UnityEngine.UI.Text are present. TextMeshPro is used.",
                    MessageType.Warning);
            }
        }

        private void OnCompilationFinished(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            _runtimeCatalog = null;
            _previewRuntime = null;
            RefreshPreview();
            Repaint();
        }
    }
}
