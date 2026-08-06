#nullable enable

using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Assets;
using GreenBox.I18n.Unity.Sprites;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GreenBox.I18n.Unity.Editor.Sprites
{
    /// <summary>
    /// Draws a localized sprite key and maintains its selected-object default-locale preview.
    /// </summary>
    internal abstract class I18nSpriteInspector : UnityEditor.Editor
    {
        private const string KeyPropertyName = "_key";
        private const string IdPropertyName = "_greenBoxI18nEntryId";

        private readonly I18nEditorAssetResolver<Sprite> _resolver = new();
        private SerializedProperty? _keyProperty;
        private string? _previewError;

        /// <summary>
        /// Gets the Unity component whose sprite is changed by this inspector.
        /// </summary>
        protected abstract Component? SpriteTarget { get; }

        /// <summary>
        /// Gets the sprite currently assigned to the target.
        /// </summary>
        protected abstract Sprite? CurrentSprite { get; }

        /// <summary>
        /// Starts catalog compilation tracking and applies the current preview.
        /// </summary>
        protected virtual void OnEnable()
        {
            _keyProperty = serializedObject.FindProperty(KeyPropertyName);
            I18nCatalogCompilationEvents.CompilationFinished += OnCompilationFinished;
            RefreshPreview();
        }

        /// <summary>
        /// Stops catalog compilation tracking.
        /// </summary>
        protected virtual void OnDisable()
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

        /// <summary>
        /// Assigns a resolved preview sprite to the concrete target.
        /// </summary>
        protected abstract void SetSprite(Sprite? sprite);

        private void RefreshPreview()
        {
            if (_keyProperty == null || !target)
            {
                return;
            }

            Component? spriteTarget = SpriteTarget;
            if (!spriteTarget)
            {
                _previewError = "The required sprite target component is missing.";
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            long id = _keyProperty.FindPropertyRelative(IdPropertyName).longValue;
            if (!_resolver.TryResolve(id, out Sprite? sprite, out _previewError))
            {
                return;
            }

            if (CurrentSprite == sprite)
            {
                return;
            }

            SetSprite(sprite);
            EditorUtility.SetDirty(spriteTarget);
            if (spriteTarget.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(spriteTarget.gameObject.scene);
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

    /// <summary>
    /// Draws and previews an I18nImage component.
    /// </summary>
    [CustomEditor(typeof(I18nImage))]
    internal sealed class I18nImageInspector : I18nSpriteInspector
    {
        private I18nImage I18nImage => (I18nImage)target;

        /// <inheritdoc />
        protected override Component? SpriteTarget => I18nImage.Target;

        /// <inheritdoc />
        protected override Sprite? CurrentSprite => I18nImage.Sprite;

        /// <inheritdoc />
        protected override void SetSprite(Sprite? sprite)
        {
            Image? image = I18nImage.Target;
            if (image)
            {
                image.sprite = sprite;
            }
        }
    }

    /// <summary>
    /// Draws and previews an I18nSpriteRenderer component.
    /// </summary>
    [CustomEditor(typeof(I18nSpriteRenderer))]
    internal sealed class I18nSpriteRendererInspector : I18nSpriteInspector
    {
        private I18nSpriteRenderer I18nSpriteRenderer => (I18nSpriteRenderer)target;

        /// <inheritdoc />
        protected override Component? SpriteTarget => I18nSpriteRenderer.Target;

        /// <inheritdoc />
        protected override Sprite? CurrentSprite => I18nSpriteRenderer.Sprite;

        /// <inheritdoc />
        protected override void SetSprite(Sprite? sprite)
        {
            SpriteRenderer? spriteRenderer = I18nSpriteRenderer.Target;
            if (spriteRenderer)
            {
                spriteRenderer.sprite = sprite;
            }
        }
    }
}
