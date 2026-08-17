#nullable enable

using System;
using UnityEngine;

namespace GreenBox.I18n.Unity.Audio
{
    /// <summary>
    /// Keeps an AudioSource clip synchronized with the localized asset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("GreenBox/i18n/I18n Audio Source")]
    [Icon("Packages/com.greenbox.i18n/Editor/Assets/ComponentIcons/GreenBox.I18n.png")]
    public sealed class I18nAudioSource : I18nComponent
    {
        private AudioSource? _target;
        private string? _lastReportedError;

        /// <summary>
        /// Gets the attached AudioSource component.
        /// </summary>
        public AudioSource? Target
        {
            get
            {
                if (!_target)
                {
                    _target = GetComponent<AudioSource>();
                }

                return _target;
            }
        }

        /// <summary>
        /// Gets the audio clip currently assigned to the target.
        /// </summary>
        public AudioClip? Clip => Target ? Target.clip : null;

        private void Awake()
        {
            _target = GetComponent<AudioSource>();
        }

        /// <inheritdoc />
        protected override void UpdateContent()
        {
            AudioClip? clip = null;
            if (Key.IsAssigned)
            {
                try
                {
                    clip = global::I18n.Asset<AudioClip>(Key);
                }
                catch (Exception exception) when (
                    exception is InvalidCastException ||
                    exception is InvalidOperationException ||
                    exception is ArgumentOutOfRangeException)
                {
                    ReportError(exception.Message);
                    return;
                }
            }

            AudioSource? target = Target;
            if (!target)
            {
                ReportError("I18nAudioSource requires an AudioSource component on the same GameObject.");
                return;
            }

            if (target.clip != clip)
            {
                target.clip = clip;
                MarkLocalizedContentDirty(target);
            }

            _lastReportedError = null;
        }

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
