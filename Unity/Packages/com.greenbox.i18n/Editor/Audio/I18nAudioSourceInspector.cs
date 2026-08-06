#nullable enable

using GreenBox.I18n.Unity.Audio;
using GreenBox.I18n.Unity.Editor.Assets;
using GreenBox.I18n.Unity.Editor.Compilation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Audio
{
    /// <summary>
    /// Draws a localized audio key and maintains its selected-object default-locale preview.
    /// </summary>
    [CustomEditor(typeof(I18nAudioSource))]
    internal sealed class I18nAudioSourceInspector : UnityEditor.Editor
    {
        private const string KeyPropertyName = "_key";
        private const string IdPropertyName = "_greenBoxI18nEntryId";

        private readonly I18nEditorAssetResolver<AudioClip> _resolver = new();
        private SerializedProperty? _keyProperty;
        private string? _previewError;

        private I18nAudioSource I18nAudioSource => (I18nAudioSource)target;

        private void OnEnable()
        {
            _keyProperty = serializedObject.FindProperty(KeyPropertyName);
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
            serializedObject.ApplyModifiedProperties();

            RefreshPreview();
            if (_previewError != null)
            {
                EditorGUILayout.HelpBox(_previewError, MessageType.Error);
            }
        }

        private void RefreshPreview()
        {
            if (_keyProperty == null || !target)
            {
                return;
            }

            AudioSource? audioSource = I18nAudioSource.Target;
            if (!audioSource)
            {
                _previewError = "The required AudioSource component is missing.";
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            long id = _keyProperty.FindPropertyRelative(IdPropertyName).longValue;
            if (!_resolver.TryResolve(id, out AudioClip? clip, out _previewError))
            {
                return;
            }

            if (audioSource.clip == clip)
            {
                return;
            }

            audioSource.clip = clip;
            EditorUtility.SetDirty(audioSource);
            if (audioSource.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(audioSource.gameObject.scene);
            }
        }

        private void OnCompilationFinished(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            _resolver.Reset();
            RefreshPreview();
            Repaint();
        }
    }
}
