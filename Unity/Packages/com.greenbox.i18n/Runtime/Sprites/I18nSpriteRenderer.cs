#nullable enable

using UnityEngine;

namespace GreenBox.I18n.Unity.Sprites
{
    /// <summary>
    /// Keeps a SpriteRenderer synchronized with the localized sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("GreenBox/i18n/I18n Sprite Renderer")]
    [Icon("Packages/com.greenbox.i18n/Editor/Assets/ComponentIcons/GreenBox.I18n.png")]
    public sealed class I18nSpriteRenderer : I18nSpriteComponent
    {
        private SpriteRenderer? _target;

        /// <summary>
        /// Gets the attached SpriteRenderer component.
        /// </summary>
        public SpriteRenderer? Target
        {
            get
            {
                if (!_target)
                {
                    _target = GetComponent<SpriteRenderer>();
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
            "I18nSpriteRenderer requires a SpriteRenderer component on the same GameObject.";

        private void Awake()
        {
            _target = GetComponent<SpriteRenderer>();
        }

        /// <inheritdoc />
        protected override bool TryApplySprite(Sprite? sprite)
        {
            SpriteRenderer? target = Target;
            if (!target)
            {
                return false;
            }

            target.sprite = sprite;
            return true;
        }
    }
}
