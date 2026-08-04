#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace GreenBox.I18n.Unity.Sprites
{
    /// <summary>
    /// Keeps a Unity UI Image synchronized with the localized sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("GreenBox/i18n/I18n Image")]
    public sealed class I18nImage : I18nSpriteComponent
    {
        private Image? _target;

        /// <summary>
        /// Gets the attached Unity UI Image component.
        /// </summary>
        public Image? Target
        {
            get
            {
                if (!_target)
                {
                    _target = GetComponent<Image>();
                }

                return _target;
            }
        }

        /// <summary>
        /// Gets the sprite currently displayed by the target.
        /// </summary>
        public Sprite? Sprite => Target ? Target.sprite : null;

        /// <inheritdoc />
        protected override string MissingTargetError =>
            "I18nImage requires a UnityEngine.UI.Image component on the same GameObject.";

        private void Awake()
        {
            _target = GetComponent<Image>();
        }

        /// <inheritdoc />
        protected override bool TryApplySprite(Sprite? sprite)
        {
            Image? target = Target;
            if (!target)
            {
                return false;
            }

            target.sprite = sprite;
            return true;
        }
    }
}
