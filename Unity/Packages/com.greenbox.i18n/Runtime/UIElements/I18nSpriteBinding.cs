#nullable enable

using System;
using GreenBox.I18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreenBox.I18n.Unity.UIElements
{
    /// <summary>
    /// Binds a UI Toolkit image or background to a localized sprite.
    /// </summary>
    [UxmlObject]
    public partial class I18nSpriteBinding : CustomBinding
    {
        private long _entryId;
        private int _activationCount;

        /// <summary>
        /// Gets or sets the stable ID of the sprite entry.
        /// </summary>
        [UxmlAttribute("entry-id")]
        public long EntryId
        {
            get => _entryId;
            set
            {
                if (_entryId == value)
                {
                    return;
                }

                _entryId = value;
                MarkDirty();
            }
        }

        public I18nSpriteBinding()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        protected override void OnActivated(in BindingActivationContext context)
        {
            if (_activationCount++ == 0)
            {
                global::I18n.LocaleChanged += HandleLocaleChanged;
            }

            MarkDirty();
        }

        protected override void OnDeactivated(in BindingActivationContext context)
        {
            if (--_activationCount == 0)
            {
                global::I18n.LocaleChanged -= HandleLocaleChanged;
            }
        }

        protected override BindingResult Update(in BindingContext context)
        {
            try
            {
                Sprite? sprite = _entryId == 0 ? null : global::I18n.Asset<Sprite>(_entryId);
                VisualElement element = context.targetElement;
                string property = context.bindingId.ToString();
                if (property == "style.backgroundImage")
                {
                    element.style.backgroundImage = sprite
                        ? new StyleBackground(sprite)
                        : new StyleBackground(StyleKeyword.None);
                    return new BindingResult(BindingStatus.Success);
                }

                if (property == "sprite" && element is Image image)
                {
                    image.sprite = sprite;
                    return new BindingResult(BindingStatus.Success);
                }

                return new BindingResult(
                    BindingStatus.Failure,
                    $"GreenBox sprite binding supports 'style.backgroundImage' and Image.sprite, not '{property}'.");
            }
            catch (Exception exception)
            {
                return new BindingResult(BindingStatus.Failure, exception.Message);
            }
        }

        private void HandleLocaleChanged(I18nRuntimeLocale _)
        {
            MarkDirty();
        }
    }
}
