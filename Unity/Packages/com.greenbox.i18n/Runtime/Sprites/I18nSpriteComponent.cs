#nullable enable

using System;
using UnityEngine;

namespace GreenBox.I18n.Unity.Sprites
{
    /// <summary>
    /// Resolves a localized sprite and delegates its application to a concrete Unity target.
    /// </summary>
    public abstract class I18nSpriteComponent : I18nComponent
    {
        private string? _lastReportedError;

        /// <summary>
        /// Resolves and applies the sprite for the current localization key.
        /// </summary>
        protected sealed override void UpdateContent()
        {
            Sprite? sprite;
            try
            {
                sprite = i18n.Asset<Sprite>(Key);
            }
            catch (Exception exception) when (
                exception is InvalidCastException ||
                exception is InvalidOperationException ||
                exception is ArgumentOutOfRangeException)
            {
                ReportError(exception.Message);
                return;
            }

            if (!TryApplySprite(sprite))
            {
                ReportError(MissingTargetError);
                return;
            }

            _lastReportedError = null;
        }

        /// <summary>
        /// Gets the error reported when the expected Unity target is absent.
        /// </summary>
        protected abstract string MissingTargetError { get; }

        /// <summary>
        /// Applies a resolved sprite to the concrete Unity target.
        /// </summary>
        /// <param name="sprite">Resolved sprite, or <see langword="null"/> when none is assigned.</param>
        /// <returns><see langword="true"/> when the target exists; otherwise, <see langword="false"/>.</returns>
        protected abstract bool TryApplySprite(Sprite? sprite);

        private void ReportError(string message)
        {
            if (string.Equals(_lastReportedError, message, StringComparison.Ordinal))
            {
                return;
            }

            _lastReportedError = message;
            Debug.LogError(message, this);
        }
    }
}
