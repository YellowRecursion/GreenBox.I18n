using System.Collections.Generic;
using UnityEngine;

namespace GreenBox.I18n.Unity
{
    /// <summary>
    /// Provides localization key storage and runtime update lifecycle for Unity components.
    /// </summary>
    public abstract class I18nComponent : MonoBehaviour
    {
        [SerializeField]
        private I18nKey _key;

        private bool _hasWarnedAboutUnassignedKey;

        /// <summary>
        /// Gets or sets the localization key used by this component.
        /// </summary>
        public I18nKey Key
        {
            get => _key;
            set
            {
                if (_key == value)
                {
                    return;
                }

                _key = value;
                Refresh();
            }
        }

        /// <summary>
        /// Updates the component from the active localization runtime.
        /// </summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!_key.IsAssigned)
            {
                WarnAboutUnassignedKey();
            }
            else
            {
                _hasWarnedAboutUnassignedKey = false;
            }

            UpdateContent();
        }

        /// <summary>
        /// Subscribes the component to localization changes and applies the current value.
        /// </summary>
        protected virtual void OnEnable()
        {
            global::I18n.LocaleChanged += HandleLocaleChanged;
            Refresh();
        }

        /// <summary>
        /// Unsubscribes the component from localization changes.
        /// </summary>
        protected virtual void OnDisable()
        {
            global::I18n.LocaleChanged -= HandleLocaleChanged;
        }

        /// <summary>
        /// Applies serialized key changes in both Edit Mode and Play Mode.
        /// </summary>
        protected virtual void OnValidate()
        {
            Refresh();
        }

        /// <summary>
        /// Resolves and applies content for the current localization key.
        /// </summary>
        protected abstract void UpdateContent();

        /// <summary>
        /// Registers an Edit Mode change made to the component that stores localized content.
        /// </summary>
        /// <remarks>
        /// Call this after changing a serialized field on a target component. It has no effect
        /// in Play Mode or in a player build.
        /// </remarks>
        protected static void MarkLocalizedContentDirty(Object target)
        {
#if UNITY_EDITOR
            if (!target || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            UnityEditor.EditorUtility.SetDirty(target);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
#endif
        }

        private void HandleLocaleChanged(I18nRuntimeLocale _)
        {
            Refresh();
        }

        private void WarnAboutUnassignedKey()
        {
            if (!Application.isPlaying || _hasWarnedAboutUnassignedKey)
            {
                return;
            }

            _hasWarnedAboutUnassignedKey = true;
            Debug.LogWarning(
                $"An unassigned localization key was requested by {GetType().Name} on " +
                $"'{GetObjectPath()}'. Returning '{(global::I18n.NonePlaceholder)}'.",
                this);
        }

        private string GetObjectPath()
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            string hierarchyPath = string.Join("/", segments);
            string sceneName = gameObject.scene.name;
            return string.IsNullOrEmpty(sceneName)
                ? hierarchyPath
                : $"{sceneName}/{hierarchyPath}";
        }
    }
}
