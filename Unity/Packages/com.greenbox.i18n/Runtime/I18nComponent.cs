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
        /// Updates the component from the active localization runtime when it is initialized.
        /// </summary>
        public void Refresh()
        {
            if (global::I18n.IsInitialized)
            {
                UpdateContent();
            }
        }

        /// <summary>
        /// Subscribes the component to localization changes and applies the current value.
        /// </summary>
        protected virtual void OnEnable()
        {
            global::I18n.LocaleChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// Unsubscribes the component from localization changes.
        /// </summary>
        protected virtual void OnDisable()
        {
            global::I18n.LocaleChanged -= Refresh;
        }

        /// <summary>
        /// Applies serialized key changes when the active runtime is available.
        /// </summary>
        protected virtual void OnValidate()
        {
            Refresh();
        }

        /// <summary>
        /// Resolves and applies content for the current localization key.
        /// </summary>
        protected abstract void UpdateContent();
    }
}
